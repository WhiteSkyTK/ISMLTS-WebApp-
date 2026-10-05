using System.Globalization;
using System.IO.Compression;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    // POPIA right of access: everything ISMLTS holds about one student, as CSV files in a ZIP.
    // Marks appear once released, like everywhere else; passwords, two-factor secrets and recovery codes never do.
    public interface IMyDataService
    {
        Task<byte[]?> ExportAsync(int studentId);
    }

    public class MyDataService : IMyDataService
    {
        private readonly IStudentRepository _students;
        private readonly IModuleRepository _modules;
        private readonly IMarkRepository _marks;
        private readonly ISubmissionRepository _submissions;
        private readonly IAttendanceRepository _attendance;
        private readonly ITicketRepository _tickets;
        private readonly ICalendarNoteRepository _notes;

        public MyDataService(
            IStudentRepository students,
            IModuleRepository modules,
            IMarkRepository marks,
            ISubmissionRepository submissions,
            IAttendanceRepository attendance,
            ITicketRepository tickets,
            ICalendarNoteRepository notes)
        {
            _students = students;
            _modules = modules;
            _marks = marks;
            _submissions = submissions;
            _attendance = attendance;
            _tickets = tickets;
            _notes = notes;
        }

        public async Task<byte[]?> ExportAsync(int studentId)
        {
            var student = await _students.GetByIdWithModulesAsync(studentId);
            if (student == null) return null;
            var codes = (await _modules.GetAllAsync()).ToDictionary(m => m.ModuleId, m => m.Code);

            var files = new Dictionary<string, IEnumerable<IEnumerable<string?>>>
            {
                ["profile.csv"] = Rows(["Name", "Email", "Programme", "Two-factor sign-in", "Modules"],
                    [[student.FullName, student.Email, student.Programme, student.TwoFactorEnabled ? "On" : "Off",
                      string.Join("; ", student.Modules.OrderBy(m => m.Code).Select(m => $"{m.Code} {m.Name}"))]]),

                ["marks.csv"] = Rows(["Module", "Assessment", "Score", "Out of", "Percentage", "Feedback", "Captured"],
                    (await _marks.GetByStudentAsync(studentId)).Where(m => m.IsVisibleToStudent).OrderBy(m => m.DateCaptured).Select(m => new[]
                    {
                        m.Module?.Code, m.AssessmentName, Number(m.Score), Number(m.MaxScore), Number(m.Percentage), m.Feedback, Date(m.DateCaptured)
                    })),

                ["submissions.csv"] = Rows(["Module", "Assessment", "Due", "Last handed in", "Status", "Link", "Files (newest first)"],
                    (await _submissions.GetByStudentAsync(studentId)).Select(s => new[]
                    {
                        s.Assessment != null ? codes.GetValueOrDefault(s.Assessment.ModuleId) : null,
                        s.Assessment?.Name,
                        s.Assessment != null ? Date(s.Assessment.DueDate) : null,
                        s.SubmittedAt is DateTime at ? Time(at) : null,
                        s.Status,
                        s.Link,
                        string.Join("; ", s.Files.OrderByDescending(f => f.UploadedAt).Select(f => $"{f.FileName} ({Time(f.UploadedAt)})"))
                    })),

                ["attendance.csv"] = Rows(["Module", "Class", "Scanned", "Marked by lecturer", "Cancelled class", "On campus network", "Location confirmed", "IP address", "Latitude", "Longitude", "Metres from class"],
                    (await _attendance.GetAllRecordsForStudentAsync(studentId)).Select(r => new[]
                    {
                        r.Session?.Module?.Code, r.Session != null ? Time(r.Session.StartedAt) : null, Time(r.ScannedAt), YesNo(r.IsManual),
                        YesNo(r.Session?.IsCancelled == true), YesNo(r.IpOnCampus), YesNo(r.LocationVerified), r.IpAddress,
                        Number(r.Latitude), Number(r.Longitude), Number(r.DistanceMeters)
                    })),

                ["tickets.csv"] = Rows(["Module", "Subject", "Question", "Status", "Lecturer's answer", "Opened", "Resolved"],
                    (await _tickets.GetByStudentAsync(studentId)).Select(t => new[]
                    {
                        t.Module?.Code, t.Subject, t.Description, t.Status, t.LecturerResponse, Time(t.DateOpened), t.DateResolved is DateTime done ? Time(done) : null
                    })),

                ["calendar-notes.csv"] = Rows(["Date", "Time", "Title", "Details", "Done", "Reminder"],
                    (await _notes.GetForUserAsync(Roles.Student, studentId, DateTime.MinValue, DateTime.MaxValue)).Select(n => new[]
                    {
                        Date(n.Date), n.Time?.ToString("HH:mm", CultureInfo.InvariantCulture), n.Title, n.Details, YesNo(n.IsDone), YesNo(n.Remind)
                    }))
            };

            using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (name, rows) in files)
                {
                    await using var entry = zip.CreateEntry(name).Open();
                    var bytes = Csv.ToUtf8WithBom(Csv.Write(rows));
                    await entry.WriteAsync(bytes);
                }
            }
            return buffer.ToArray();
        }

        private static IEnumerable<IEnumerable<string?>> Rows(string[] header, IEnumerable<IEnumerable<string?>> rows) =>
            new[] { header.AsEnumerable<string?>() }.Concat(rows);

        private static string Date(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // Stored in UTC, written in college time
        private static string Time(DateTime utc) =>
            DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        private static string? Number(decimal? value) => value?.ToString(CultureInfo.InvariantCulture);

        private static string? Number(double? value) => value?.ToString(CultureInfo.InvariantCulture);

        private static string YesNo(bool value) => value ? "Yes" : "No";
    }
}
