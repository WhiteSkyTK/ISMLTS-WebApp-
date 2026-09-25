using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    public class AttendanceController : Controller
    {
        // No 0/O or 1/I, so typed codes can't be misread
        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const string ScanErrorKey = "ScanError";

        private readonly IAttendanceRepository _attendanceRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IAttendanceVerifier _verifier;
        private readonly IQrCodeService _qrCodeService;
        private readonly AttendanceOptions _options;

        public AttendanceController(
            IAttendanceRepository attendanceRepository,
            IModuleRepository moduleRepository,
            IStudentRepository studentRepository,
            IAttendanceVerifier verifier,
            IQrCodeService qrCodeService,
            IOptions<AttendanceOptions> options)
        {
            _attendanceRepository = attendanceRepository;
            _moduleRepository = moduleRepository;
            _studentRepository = studentRepository;
            _verifier = verifier;
            _qrCodeService = qrCodeService;
            _options = options.Value;
        }

        // ---------- Lecturer ----------

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Index() =>
            View(await _moduleRepository.GetByLecturerAsync(User.GetUserId() ?? 0));

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> ForModule(int moduleId)
        {
            var module = await GetOwnedModuleAsync(moduleId);
            if (module == null) return NotFound();

            ViewBag.ModuleDisplay = $"{module.Code} - {module.Name}";
            ViewBag.ModuleId = moduleId;
            return View(await _attendanceRepository.GetByModuleAsync(moduleId));
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int moduleId, double? latitude, double? longitude)
        {
            if (await GetOwnedModuleAsync(moduleId) == null) return NotFound();

            string code;
            do
            {
                code = RandomNumberGenerator.GetString(CodeAlphabet, 6);
            }
            while (await _attendanceRepository.CodeExistsAsync(code));

            var session = new AttendanceSession
            {
                ModuleId = moduleId,
                Code = code,
                StartedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_options.SessionMinutes),
                Latitude = latitude,
                Longitude = longitude
            };
            await _attendanceRepository.AddAsync(session);
            await _attendanceRepository.SaveChangesAsync();
            return RedirectToAction(nameof(Live), new { id = session.SessionId });
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Live(int id)
        {
            var session = await GetOwnedSessionAsync(id);
            if (session == null) return NotFound();

            var scanUrl = Url.Action(nameof(Scan), "Attendance", new { code = session.Code }, Request.Scheme) ?? string.Empty;
            return View(new AttendanceLiveViewModel
            {
                Session = session,
                ScanUrl = scanUrl,
                QrDataUri = _qrCodeService.ToPngDataUri(scanUrl),
                SecondsLeft = session.IsOpen ? (int)(session.ExpiresAt - DateTime.UtcNow).TotalSeconds : 0
            });
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Records(int id)
        {
            var session = await GetOwnedSessionAsync(id);
            return session == null ? NotFound() : PartialView("_Records", session);
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id)
        {
            var session = await GetOwnedSessionAsync(id);
            if (session == null) return NotFound();

            session.IsClosed = true; // tracked entity, so SaveChanges picks this up
            await _attendanceRepository.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Details(int id)
        {
            var session = await GetOwnedSessionAsync(id);
            if (session == null) return NotFound();

            var presentIds = session.Records.Select(r => r.StudentId).ToHashSet();
            var enrolled = await _studentRepository.GetByModuleAsync(session.ModuleId);
            return View(new AttendanceDetailsViewModel
            {
                Session = session,
                Absent = enrolled.Where(s => !presentIds.Contains(s.StudentId)).OrderBy(s => s.FullName).ToList()
            });
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPresent(int id, int studentId)
        {
            var session = await GetOwnedSessionAsync(id);
            if (session == null) return NotFound();

            var enrolled = await _studentRepository.GetByModuleAsync(session.ModuleId);
            if (enrolled.Any(s => s.StudentId == studentId) && session.Records.All(r => r.StudentId != studentId))
            {
                await _attendanceRepository.AddRecordAsync(new AttendanceRecord
                {
                    SessionId = id,
                    StudentId = studentId,
                    ScannedAt = DateTime.UtcNow,
                    IsManual = true
                });
                await _attendanceRepository.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRecord(int id, int recordId)
        {
            var session = await GetOwnedSessionAsync(id);
            var record = session?.Records.FirstOrDefault(r => r.RecordId == recordId);
            if (record == null) return NotFound();

            _attendanceRepository.RemoveRecord(record);
            await _attendanceRepository.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        // ---------- Student ----------

        [Authorize(Roles = "Student")]
        [HttpGet]
        public IActionResult Scan(string? code) => View(model: code?.Trim().ToUpperInvariant());

        [Authorize(Roles = "Student")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Scan(string? code, double? latitude, double? longitude, double? accuracy)
        {
            var normalised = code?.Trim().ToUpperInvariant() ?? string.Empty;
            var studentId = User.GetUserId();
            var session = await _attendanceRepository.GetByCodeAsync(normalised);
            if (session == null || studentId == null)
            {
                return ScanFailed(normalised, "That code doesn't match an attendance session. Check it and try again.");
            }

            var problem = await FindScanProblemAsync(session, studentId.Value);
            if (problem != null)
            {
                return ScanFailed(normalised, problem);
            }

            var ip = HttpContext.Connection.RemoteIpAddress;
            var check = _verifier.Verify(session, ip, latitude, longitude);
            if (!check.Passed)
            {
                return ScanFailed(normalised, "We couldn't confirm you're in class. Connect to the campus Wi-Fi or allow location access, then try again.");
            }

            await _attendanceRepository.AddRecordAsync(new AttendanceRecord
            {
                SessionId = session.SessionId,
                StudentId = studentId.Value,
                ScannedAt = DateTime.UtcNow,
                IpAddress = ip?.ToString(),
                IpOnCampus = check.IpOnCampus,
                Latitude = latitude,
                Longitude = longitude,
                AccuracyMeters = accuracy,
                DistanceMeters = check.DistanceMeters,
                LocationVerified = check.LocationVerified
            });
            await _attendanceRepository.SaveChangesAsync();

            TempData["ScanSuccess"] = $"You're marked present for {session.Module?.Code}.";
            return RedirectToAction(nameof(MyAttendance));
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyAttendance()
        {
            var studentId = User.GetUserId();
            if (studentId == null) return Forbid();

            var student = await _studentRepository.GetByIdWithModulesAsync(studentId.Value);
            var attendedByModule = (await _attendanceRepository.GetRecordsByStudentAsync(studentId.Value))
                .GroupBy(r => r.Session?.ModuleId ?? 0)
                .ToDictionary(g => g.Key, g => g.Count());

            var rows = new List<MyAttendanceRow>();
            foreach (var module in student?.Modules ?? new List<Module>())
            {
                rows.Add(new MyAttendanceRow
                {
                    ModuleDisplay = $"{module.Code} - {module.Name}",
                    Attended = attendedByModule.GetValueOrDefault(module.ModuleId),
                    Total = await _attendanceRepository.CountByModuleAsync(module.ModuleId)
                });
            }
            return View(rows);
        }

        // ---------- Helpers ----------

        private RedirectToActionResult ScanFailed(string code, string message)
        {
            TempData[ScanErrorKey] = message;
            return RedirectToAction(nameof(Scan), new { code });
        }

        private async Task<string?> FindScanProblemAsync(AttendanceSession session, int studentId)
        {
            if (!session.IsOpen) return "This attendance session has closed.";

            var enrolled = await _studentRepository.GetByModuleAsync(session.ModuleId);
            if (enrolled.All(s => s.StudentId != studentId)) return "You're not enrolled in this module.";

            return await _attendanceRepository.HasScannedAsync(session.SessionId, studentId)
                ? "You're already marked present for this session."
                : null;
        }

        private async Task<Module?> GetOwnedModuleAsync(int moduleId)
        {
            var module = await _moduleRepository.GetByIdAsync(moduleId);
            return module != null && module.LecturerId == User.GetUserId() ? module : null;
        }

        private async Task<AttendanceSession?> GetOwnedSessionAsync(int sessionId)
        {
            var session = await _attendanceRepository.GetWithRecordsAsync(sessionId);
            return session?.Module != null && session.Module.LecturerId == User.GetUserId() ? session : null;
        }
    }
}