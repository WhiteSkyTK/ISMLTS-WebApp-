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
    [Authorize]
    public class AttendanceController : Controller
    {
        // No 0/O or 1/I, so typed codes can't be misread
        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const string ScanErrorKey = "ScanError";

        private readonly IAttendanceRepository _attendanceRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IStudentPortalService _portal;
        private readonly IQrCodeService _qrCodeService;
        private readonly AttendanceOptions _options;
        private readonly INotificationService _notifications;
        private readonly ITimetableRepository _timetable;

        public AttendanceController(
            IAttendanceRepository attendanceRepository,
            IModuleRepository moduleRepository,
            IStudentRepository studentRepository,
            IStudentPortalService portal,
            IQrCodeService qrCodeService,
            IOptions<AttendanceOptions> options,
            INotificationService notifications,
            ITimetableRepository timetable)
        {
            _attendanceRepository = attendanceRepository;
            _moduleRepository = moduleRepository;
            _studentRepository = studentRepository;
            _portal = portal;
            _qrCodeService = qrCodeService;
            _options = options.Value;
            _notifications = notifications;
            _timetable = timetable;
        }

        // ---------- Lecturer ----------

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Index()
        {
            var modules = (await _moduleRepository.GetByLecturerAsync(User.GetUserId() ?? 0)).ToList();
            // The class on now (from the timetable) gets a one-click "Take register"
            ViewBag.NowSlot = Timetable.Now(await _timetable.GetByModulesAsync(modules.Select(m => m.ModuleId).ToList()), DateTime.Now);
            return View(modules);
        }

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
            var module = await GetOwnedModuleAsync(moduleId);
            if (module == null) return NotFound();

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
            await _notifications.AttendanceOpenedAsync(module);
            this.Toast($"Attendance is open for {_options.SessionMinutes} minutes. Show this QR code to the class.", ToastTypes.Info);
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
            this.Toast("Session closed. Anyone who missed it can be marked present here.");
            return RedirectToAction(nameof(Details), new { id });
        }

        // A class that didn't happen after all stays on the list but stops counting towards anyone's attendance
        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCancelled(int id, bool cancelled)
        {
            var session = await GetOwnedSessionAsync(id);
            if (session == null) return NotFound();

            session.IsCancelled = cancelled;
            if (cancelled) session.IsClosed = true;
            await _attendanceRepository.SaveChangesAsync();
            this.Toast(cancelled
                ? "Marked as a cancelled class. It no longer counts towards attendance."
                : "This class counts towards attendance again.", ToastTypes.Info);
            return RedirectToAction(nameof(ForModule), new { moduleId = session.ModuleId });
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

        // The register as a spreadsheet: everyone enrolled, present or absent
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Export(int id)
        {
            var session = await GetOwnedSessionAsync(id);
            if (session == null) return NotFound();

            var csv = Csv.RegisterExport(session, await _studentRepository.GetByModuleAsync(session.ModuleId));
            var started = session.StartedAt.ToLocalTime();
            return File(Csv.ToUtf8WithBom(csv), "text/csv", $"{session.Module?.Code}-register-{started:yyyy-MM-dd-HHmm}.csv");
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
                await _notifications.MarkedPresentAsync(session.Module!, studentId);
                this.Toast($"{enrolled.First(s => s.StudentId == studentId).FullName} was marked present.");
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
            await _notifications.ScanRemovedAsync(session!.Module!, record.StudentId);
            this.Toast($"{record.Student?.FullName} was removed from this register.", ToastTypes.Info);
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
            if (User.GetUserId() is not int studentId) return Forbid();

            var result = await _portal.ScanAsync(studentId, code, HttpContext.Connection.RemoteIpAddress, latitude, longitude, accuracy);
            if (!result.Present)
            {
                TempData[ScanErrorKey] = result.Message;
                return RedirectToAction(nameof(Scan), new { code = code?.Trim().ToUpperInvariant() ?? string.Empty });
            }

            this.Toast(result.Message);
            return RedirectToAction(nameof(MyAttendance));
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyAttendance()
        {
            if (User.GetUserId() is not int studentId) return Forbid();
            return View(await _portal.AttendanceAsync(studentId));
        }

        // ---------- Helpers ----------

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