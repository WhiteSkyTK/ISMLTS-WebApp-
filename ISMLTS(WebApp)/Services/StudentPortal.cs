using System.Net;
using Microsoft.Extensions.Options;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    public enum SubmitOutcome { NotFound, BadLink, Saved }

    public record SubmitResult(SubmitOutcome Outcome, Assessment? Assessment);

    public record TicketResult(Ticket? Ticket, string? Field, string? Error);

    // Code says what went wrong for the app; Message is what the student reads
    public record ScanResult(bool Present, string Code, string Message, Module? Module);

    public static class ScanCodes
    {
        public const string Present = "present";
        public const string UnknownCode = "unknown_code";
        public const string Closed = "session_closed";
        public const string NotEnrolled = "not_enrolled";
        public const string AlreadyPresent = "already_present";
        public const string NotInClass = "not_in_class";
    }

    // What a student does and sees, shared by the website and the Android app's API.
    // Every method works only on the given student's own records.
    public interface IStudentPortalService
    {
        Task<Student?> StudentAsync(int studentId);
        Task<List<MyAssessmentRow>> AssessmentsAsync(int studentId);
        Task<MyProgressViewModel?> ProgressAsync(int studentId);
        Task<List<MyAttendanceRow>> AttendanceAsync(int studentId);
        Task<Assessment?> EnrolledAssessmentAsync(int studentId, int assessmentId);
        Task<SubmitResult> SubmitAsync(int studentId, int assessmentId, string? link);
        Task<TicketResult> RaiseTicketAsync(int studentId, int moduleId, string? subject, string? description);
        Task<ScanResult> ScanAsync(int studentId, string? code, IPAddress? ip, double? latitude, double? longitude, double? accuracy);
    }

    public class StudentPortalService : IStudentPortalService
    {
        public const string BadLinkMessage = "Paste the full link to your work, starting with https://";

        private readonly IStudentRepository _students;
        private readonly IAssessmentRepository _assessments;
        private readonly ISubmissionRepository _submissions;
        private readonly IMarkRepository _marks;
        private readonly IAttendanceRepository _attendance;
        private readonly ITicketRepository _tickets;
        private readonly INotificationService _notifications;
        private readonly IAttendanceVerifier _verifier;
        private readonly RiskOptions _risk;

        public StudentPortalService(
            IStudentRepository students,
            IAssessmentRepository assessments,
            ISubmissionRepository submissions,
            IMarkRepository marks,
            IAttendanceRepository attendance,
            ITicketRepository tickets,
            INotificationService notifications,
            IAttendanceVerifier verifier,
            IOptions<RiskOptions> risk)
        {
            _students = students;
            _assessments = assessments;
            _submissions = submissions;
            _marks = marks;
            _attendance = attendance;
            _tickets = tickets;
            _notifications = notifications;
            _verifier = verifier;
            _risk = risk.Value;
        }

        public Task<Student?> StudentAsync(int studentId) => _students.GetByIdWithModulesAsync(studentId);

        // Every assessment in the student's modules, soonest first, with their submission and released mark
        public async Task<List<MyAssessmentRow>> AssessmentsAsync(int studentId)
        {
            var student = await _students.GetByIdWithModulesAsync(studentId);
            if (student == null) return new List<MyAssessmentRow>();

            var moduleIds = student.Modules.Select(m => m.ModuleId).ToList();
            var codes = student.Modules.ToDictionary(m => m.ModuleId, m => m.Code);
            var mySubmissions = (await _submissions.GetByStudentAsync(studentId)).ToDictionary(s => s.AssessmentId);
            var myMarks = (await _marks.GetByStudentAsync(studentId))
                .Where(m => m.AssessmentId != null && m.IsVisibleToStudent)
                .GroupBy(m => m.AssessmentId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.MarkId).First());

            return (await _assessments.GetByModulesAsync(moduleIds)).Select(a =>
            {
                mySubmissions.TryGetValue(a.AssessmentId, out var sub);
                myMarks.TryGetValue(a.AssessmentId, out var mark);
                return new MyAssessmentRow
                {
                    AssessmentId = a.AssessmentId,
                    ModuleId = a.ModuleId,
                    Name = a.Name,
                    ModuleCode = codes.GetValueOrDefault(a.ModuleId, string.Empty),
                    Type = a.Type,
                    Description = a.Description,
                    DueDate = a.DueDate,
                    MaxScore = a.MaxScore,
                    Status = Submission.StatusFor(sub?.SubmittedAt, a.DueDate),
                    SubmittedAt = sub?.SubmittedAt,
                    Link = sub?.Link,
                    Mark = mark,
                    MarksReleased = a.MarksReleased
                };
            }).OrderBy(r => r.DueDate).ThenBy(r => r.Name).ToList();
        }

        public async Task<MyProgressViewModel?> ProgressAsync(int studentId)
        {
            var student = await _students.GetByIdWithModulesAsync(studentId);
            if (student == null) return null;

            var moduleIds = student.Modules.Select(m => m.ModuleId).ToList();
            var marks = (await _marks.GetByStudentAsync(studentId)).Where(m => m.IsVisibleToStudent).ToList();
            var attendedByModule = await AttendedByModuleAsync(studentId);
            var sessionsByModule = await _attendance.CountSessionsByModuleAsync(moduleIds);
            var assessments = await _assessments.GetByModulesAsync(moduleIds);
            var submitted = (await _submissions.GetByStudentAsync(studentId))
                .Where(s => s.SubmittedAt != null)
                .Select(s => s.AssessmentId)
                .ToHashSet();

            var modules = student.Modules.OrderBy(m => m.Term).ThenBy(m => m.Code).Select(m => Progress.ForModule(
                new StudentModuleData(
                    m,
                    marks.Where(x => x.ModuleId == m.ModuleId).ToList(),
                    sessionsByModule.GetValueOrDefault(m.ModuleId),
                    attendedByModule.GetValueOrDefault(m.ModuleId),
                    assessments.Where(a => a.ModuleId == m.ModuleId).ToList(),
                    submitted),
                DateTime.Today,
                _risk.AttendanceThreshold)).ToList();

            return new MyProgressViewModel
            {
                Modules = modules,
                Summary = Progress.Summarise(modules),
                AttendanceThreshold = _risk.AttendanceThreshold
            };
        }

        public async Task<List<MyAttendanceRow>> AttendanceAsync(int studentId)
        {
            var student = await _students.GetByIdWithModulesAsync(studentId);
            if (student == null) return new List<MyAttendanceRow>();

            var attendedByModule = await AttendedByModuleAsync(studentId);
            var sessionsByModule = await _attendance.CountSessionsByModuleAsync(student.Modules.Select(m => m.ModuleId).ToList());
            return student.Modules.OrderBy(m => m.Code).Select(m => new MyAttendanceRow
            {
                ModuleId = m.ModuleId,
                ModuleCode = m.Code,
                ModuleDisplay = $"{m.Code} - {m.Name}",
                Attended = attendedByModule.GetValueOrDefault(m.ModuleId),
                Total = sessionsByModule.GetValueOrDefault(m.ModuleId)
            }).ToList();
        }

        // An assessment in a module the student is enrolled in; anything else looks like it doesn't exist
        public async Task<Assessment?> EnrolledAssessmentAsync(int studentId, int assessmentId)
        {
            var assessment = await _assessments.GetByIdWithModuleAsync(assessmentId);
            return assessment != null && await _students.IsEnrolledAsync(studentId, assessment.ModuleId) ? assessment : null;
        }

        // Hands in (or replaces) the link to the student's work; only http(s) links are accepted
        public async Task<SubmitResult> SubmitAsync(int studentId, int assessmentId, string? link)
        {
            var assessment = await EnrolledAssessmentAsync(studentId, assessmentId);
            if (assessment == null) return new SubmitResult(SubmitOutcome.NotFound, null);

            link = link?.Trim();
            if (!LinkValidator.IsWebLink(link)) return new SubmitResult(SubmitOutcome.BadLink, assessment);

            var existing = await _submissions.GetByAssessmentAndStudentAsync(assessmentId, studentId);
            if (existing == null)
            {
                await _submissions.AddAsync(new Submission { AssessmentId = assessmentId, StudentId = studentId, Link = link, SubmittedAt = DateTime.UtcNow });
            }
            else
            {
                existing.Link = link;
                existing.SubmittedAt = DateTime.UtcNow;
                _submissions.Update(existing);
            }
            await _submissions.SaveChangesAsync();
            return new SubmitResult(SubmitOutcome.Saved, assessment);
        }

        public async Task<TicketResult> RaiseTicketAsync(int studentId, int moduleId, string? subject, string? description)
        {
            subject = subject?.Trim() ?? string.Empty;
            description = description?.Trim() ?? string.Empty;
            if (!await _students.IsEnrolledAsync(studentId, moduleId))
                return new TicketResult(null, nameof(Ticket.ModuleId), "Pick one of your modules.");
            if (subject.Length == 0 || subject.Length > 150)
                return new TicketResult(null, nameof(Ticket.Subject), "Give your question a subject of up to 150 characters.");
            if (description.Length == 0 || description.Length > 1000)
                return new TicketResult(null, nameof(Ticket.Description), "Describe your question in up to 1000 characters.");

            var ticket = new Ticket { StudentId = studentId, ModuleId = moduleId, Subject = subject, Description = description };
            await _tickets.AddAsync(ticket);
            await _tickets.SaveChangesAsync();

            var saved = await _tickets.GetByIdWithDetailsAsync(ticket.TicketId);
            if (saved?.Module != null)
            {
                await _notifications.TicketRaisedAsync(saved, saved.Module, saved.Student?.FullName ?? "A student");
            }
            return new TicketResult(saved ?? ticket, null, null);
        }

        // The same checks for the website and the app: an open session, enrolled, not already present, and either on
        // the campus network or close enough to where the lecturer started the register
        public async Task<ScanResult> ScanAsync(int studentId, string? code, IPAddress? ip, double? latitude, double? longitude, double? accuracy)
        {
            var session = await _attendance.GetByCodeAsync(code?.Trim().ToUpperInvariant() ?? string.Empty);
            if (session == null)
                return new ScanResult(false, ScanCodes.UnknownCode, "That code doesn't match an attendance session. Check it and try again.", null);
            if (!session.IsOpen)
                return new ScanResult(false, ScanCodes.Closed, "This attendance session has closed.", session.Module);
            if (!await _students.IsEnrolledAsync(studentId, session.ModuleId))
                return new ScanResult(false, ScanCodes.NotEnrolled, "You're not enrolled in this module.", session.Module);
            if (await _attendance.HasScannedAsync(session.SessionId, studentId))
                return new ScanResult(false, ScanCodes.AlreadyPresent, "You're already marked present for this session.", session.Module);

            var check = _verifier.Verify(session, ip, latitude, longitude);
            if (!check.Passed)
                return new ScanResult(false, ScanCodes.NotInClass, "We couldn't confirm you're in class. Connect to the campus Wi-Fi or allow location access, then try again.", session.Module);

            await _attendance.AddRecordAsync(new AttendanceRecord
            {
                SessionId = session.SessionId,
                StudentId = studentId,
                ScannedAt = DateTime.UtcNow,
                IpAddress = ip?.ToString(),
                IpOnCampus = check.IpOnCampus,
                Latitude = latitude,
                Longitude = longitude,
                AccuracyMeters = accuracy,
                DistanceMeters = check.DistanceMeters,
                LocationVerified = check.LocationVerified
            });
            await _attendance.SaveChangesAsync();
            return new ScanResult(true, ScanCodes.Present, $"You're marked present for {session.Module?.Code}.", session.Module);
        }

        private async Task<Dictionary<int, int>> AttendedByModuleAsync(int studentId) =>
            (await _attendance.GetRecordsByStudentAsync(studentId))
                .Where(r => r.Session != null)
                .GroupBy(r => r.Session!.ModuleId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.SessionId).Distinct().Count());
    }
}
