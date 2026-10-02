using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    public enum CalendarKind { Class, Due, Closing, Term, College, Note }

    // One thing on someone's calendar. Start/End are local times; End is null for all-day entries, which run from
    // Start's date to LastDay (or just that day). Badge is the college date's kind; NoteId/Done belong to notes.
    public record CalendarEntry(string Uid, CalendarKind Kind, DateTime Start, DateTime? End, string Title, string? Location)
    {
        public bool AllDay => End == null;
        public DateTime? LastDay { get; init; }
        public string? Badge { get; init; }
        public int? NoteId { get; init; }
        public bool Done { get; init; }
        public string? Details { get; init; }

        public bool IsOn(DateTime day) => day.Date >= Start.Date && day.Date <= (LastDay ?? Start).Date;
    }

    // Everything the calendar draws from, for one person
    public record CalendarSources(
        IReadOnlyCollection<Module> Modules,
        IEnumerable<TimetableSlot> Slots,
        IEnumerable<Assessment> Assessments,
        IReadOnlyList<Term> Terms,
        IEnumerable<CollegeDate> CollegeDates,
        IEnumerable<CalendarNote> Notes);

    public static class CalendarBuilder
    {
        // Classes (weekly ones only inside their module's term once terms exist; extra classes on their date), due and
        // closing dates, term and college dates, and the person's own notes, for the days from `from` up to `to`
        public static List<CalendarEntry> Build(CalendarSources s, DateTime from, DateTime to)
        {
            var entries = new List<CalendarEntry>();
            var moduleById = s.Modules.ToDictionary(m => m.ModuleId);
            var slots = s.Slots.Where(x => moduleById.ContainsKey(x.ModuleId)).ToList();

            for (var day = from.Date; day < to.Date; day = day.AddDays(1))
            {
                foreach (var slot in slots.Where(x => x.OnDate == null ? x.Day == day.DayOfWeek : x.OnDate.Value.Date == day))
                {
                    var module = moduleById[slot.ModuleId];
                    if (slot.OnDate == null && s.Terms.Any(t => t.Code == module.Term) && Terms.Containing(s.Terms, module.Term, day) == null) continue;
                    entries.Add(new CalendarEntry(
                        string.Create(CultureInfo.InvariantCulture, $"class-{slot.SlotId}-{day:yyyyMMdd}"),
                        CalendarKind.Class,
                        day + slot.StartTime.ToTimeSpan(),
                        day + slot.EndTime.ToTimeSpan(),
                        slot.OnDate == null ? $"{module.Code} class" : $"{module.Code} extra class",
                        slot.Venue));
                }
            }

            foreach (var a in s.Assessments.Where(a => moduleById.ContainsKey(a.ModuleId)))
            {
                var code = moduleById[a.ModuleId].Code;
                if (InRange(a.DueDate, from, to))
                {
                    entries.Add(new CalendarEntry(string.Create(CultureInfo.InvariantCulture, $"due-{a.AssessmentId}"), CalendarKind.Due, a.DueDate.Date, null,
                        a.LateDays == 0 ? $"{code}: {a.Name} due (no late work)" : $"{code}: {a.Name} due", null));
                }
                if (a.LateDays is > 0 && SubmissionWindow.LastDay(a.DueDate, a.LateDays) is DateTime last && InRange(last, from, to))
                {
                    entries.Add(new CalendarEntry(string.Create(CultureInfo.InvariantCulture, $"close-{a.AssessmentId}"), CalendarKind.Closing, last, null,
                        $"{code}: {a.Name} closes", null));
                }
            }

            foreach (var term in s.Terms)
            {
                if (InRange(term.StartDate, from, to))
                    entries.Add(new CalendarEntry(string.Create(CultureInfo.InvariantCulture, $"term-{term.TermId}-start"), CalendarKind.Term, term.StartDate.Date, null, $"{term.Name} starts", null));
                if (InRange(term.EndDate, from, to))
                    entries.Add(new CalendarEntry(string.Create(CultureInfo.InvariantCulture, $"term-{term.TermId}-end"), CalendarKind.Term, term.EndDate.Date, null, $"{term.Name} ends", null));
            }

            entries.AddRange(s.CollegeDates.Where(d => d.StartDate.Date < to.Date && d.EndDate.Date >= from.Date).Select(d =>
                new CalendarEntry(string.Create(CultureInfo.InvariantCulture, $"college-{d.CollegeDateId}"), CalendarKind.College, d.StartDate.Date, null, d.Title, null)
                {
                    LastDay = d.EndDate.Date > d.StartDate.Date ? d.EndDate.Date : null,
                    Badge = d.Kind
                }));

            entries.AddRange(s.Notes.Where(n => InRange(n.Date, from, to)).Select(n =>
                new CalendarEntry(string.Create(CultureInfo.InvariantCulture, $"note-{n.NoteId}"), CalendarKind.Note,
                    n.Time is TimeOnly t ? n.Date.Date + t.ToTimeSpan() : n.Date.Date,
                    n.Time is TimeOnly t2 ? n.Date.Date + t2.ToTimeSpan().Add(TimeSpan.FromMinutes(30)) : null,
                    n.Title, null)
                {
                    NoteId = n.NoteId,
                    Done = n.IsDone,
                    Details = n.Details
                }));

            return entries.OrderBy(e => e.Start.Date).ThenBy(e => e.AllDay ? 0 : 1).ThenBy(e => e.Start).ThenBy(e => e.Title).ToList();
        }

        public static string BadgeLabel(string? kind) =>
            CollegeDateKinds.All.FirstOrDefault(k => k.Kind == kind).Label ?? "College";

        private static bool InRange(DateTime day, DateTime from, DateTime to) => day.Date >= from.Date && day.Date < to.Date;
    }

    // An iCalendar (RFC 5545) file that phone calendars can subscribe to
    public static class IcsCalendar
    {
        public static string Write(string name, IEnumerable<CalendarEntry> entries, DateTime nowUtc)
        {
            var lines = new List<string>
            {
                "BEGIN:VCALENDAR",
                "VERSION:2.0",
                "PRODID:-//IIE Rosebank College//ISMLTS//EN",
                "CALSCALE:GREGORIAN",
                "METHOD:PUBLISH",
                "X-WR-CALNAME:" + Escape(name),
                "REFRESH-INTERVAL;VALUE=DURATION:PT6H",
                "X-PUBLISHED-TTL:PT6H"
            };
            var stamp = Utc(nowUtc);
            foreach (var e in entries)
            {
                lines.Add("BEGIN:VEVENT");
                lines.Add($"UID:{e.Uid}@ismlts");
                lines.Add("DTSTAMP:" + stamp);
                if (e.AllDay)
                {
                    lines.Add("DTSTART;VALUE=DATE:" + e.Start.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                    lines.Add("DTEND;VALUE=DATE:" + (e.LastDay ?? e.Start).AddDays(1).ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                }
                else
                {
                    // Local college time written as UTC, so phones in any time zone show the right moment
                    lines.Add("DTSTART:" + Utc(DateTime.SpecifyKind(e.Start, DateTimeKind.Local).ToUniversalTime()));
                    lines.Add("DTEND:" + Utc(DateTime.SpecifyKind(e.End!.Value, DateTimeKind.Local).ToUniversalTime()));
                }
                lines.Add("SUMMARY:" + Escape(e.Title));
                if (!string.IsNullOrEmpty(e.Location)) lines.Add("LOCATION:" + Escape(e.Location));
                if (!string.IsNullOrEmpty(e.Details)) lines.Add("DESCRIPTION:" + Escape(e.Details));
                lines.Add("END:VEVENT");
            }
            lines.Add("END:VCALENDAR");

            var text = new StringBuilder();
            foreach (var line in lines) Fold(text, line);
            return text.ToString();
        }

        public static string Escape(string value) =>
            value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");

        private static string Utc(DateTime utc) => utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

        // Lines longer than 75 bytes continue on the next line after a space; every line ends in CRLF
        private static void Fold(StringBuilder text, string line)
        {
            var bytes = 0;
            foreach (var c in line)
            {
                var size = Encoding.UTF8.GetByteCount([c]);
                if (bytes + size > 75)
                {
                    text.Append("\r\n ");
                    bytes = 1;
                }
                text.Append(c);
                bytes += size;
            }
            text.Append("\r\n");
        }
    }

    public interface ICalendarService
    {
        Task<List<CalendarEntry>> ForUserAsync(string role, int userId, DateTime from, DateTime to);

        // Notes and to-dos belong to one person; another person's note looks like it doesn't exist
        Task<(string Field, string Message)?> AddNoteAsync(string role, int userId, CalendarNote note);
        Task<CalendarNote?> SetNoteDoneAsync(string role, int userId, int noteId, bool done);
        Task<CalendarNote?> DeleteNoteAsync(string role, int userId, int noteId);

        // A new secret for the student's feed link; the old link stops working. Only its hash is kept.
        Task<string?> NewFeedTokenAsync(int studentId);
        Task<string?> FeedAsync(string? token);
    }

    public class CalendarService : ICalendarService
    {
        // The feed covers the last week and the next four months of classes, and every date from a month back
        private const int FeedDaysBack = 7;
        private const int FeedDaysAhead = 120;

        private readonly IStudentRepository _students;
        private readonly IModuleRepository _modules;
        private readonly IAssessmentRepository _assessments;
        private readonly ITimetableRepository _timetable;
        private readonly ITermRepository _terms;
        private readonly ICollegeDateRepository _collegeDates;
        private readonly ICalendarNoteRepository _notes;
        private readonly TimeProvider _time;

        public CalendarService(
            IStudentRepository students,
            IModuleRepository modules,
            IAssessmentRepository assessments,
            ITimetableRepository timetable,
            ITermRepository terms,
            ICollegeDateRepository collegeDates,
            ICalendarNoteRepository notes,
            TimeProvider time)
        {
            _students = students;
            _modules = modules;
            _assessments = assessments;
            _timetable = timetable;
            _terms = terms;
            _collegeDates = collegeDates;
            _notes = notes;
            _time = time;
        }

        // Students: their modules. Lecturers: the modules they teach. Admins: every module's due dates, no classes.
        public async Task<List<CalendarEntry>> ForUserAsync(string role, int userId, DateTime from, DateTime to)
        {
            List<Module> modules = role switch
            {
                Roles.Student => (await _students.GetByIdWithModulesAsync(userId))?.Modules.ToList() ?? new List<Module>(),
                Roles.Lecturer => (await _modules.GetByLecturerAsync(userId)).ToList(),
                _ => (await _modules.GetAllAsync()).ToList()
            };
            var ids = modules.Select(m => m.ModuleId).ToList();
            return CalendarBuilder.Build(new CalendarSources(
                modules,
                role == Roles.Admin ? [] : await _timetable.GetByModulesAsync(ids),
                await _assessments.GetByModulesAsync(ids),
                await _terms.GetOrderedAsync(),
                await _collegeDates.GetOverlappingAsync(from, to),
                await _notes.GetForUserAsync(role, userId, from, to)), from, to);
        }

        public async Task<(string Field, string Message)?> AddNoteAsync(string role, int userId, CalendarNote note)
        {
            note.Title = note.Title?.Trim() ?? string.Empty;
            note.Details = string.IsNullOrWhiteSpace(note.Details) ? null : note.Details.Trim();
            if (note.Title.Length is 0 or > 120) return (nameof(CalendarNote.Title), "Give the note a title of up to 120 characters.");
            if (note.Details?.Length > 500) return (nameof(CalendarNote.Details), "Keep the details to 500 characters.");
            if (note.Date.Year < 2000) return (nameof(CalendarNote.Date), "Pick a date.");

            note.NoteId = 0;
            note.Role = role;
            note.UserId = userId;
            note.Date = note.Date.Date;
            note.IsDone = false;
            note.RemindedAt = null;
            await _notes.AddAsync(note);
            await _notes.SaveChangesAsync();
            return null;
        }

        public async Task<CalendarNote?> SetNoteDoneAsync(string role, int userId, int noteId, bool done)
        {
            var note = await _notes.GetOwnedAsync(noteId, role, userId);
            if (note == null) return null;
            note.IsDone = done;
            await _notes.SaveChangesAsync();
            return note;
        }

        public async Task<CalendarNote?> DeleteNoteAsync(string role, int userId, int noteId)
        {
            var note = await _notes.GetOwnedAsync(noteId, role, userId);
            if (note == null) return null;
            _notes.Delete(note);
            await _notes.SaveChangesAsync();
            return note;
        }

        public async Task<string?> NewFeedTokenAsync(int studentId)
        {
            var student = await _students.GetByIdAsync(studentId);
            if (student == null) return null;

            var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            student.CalendarTokenHash = Hash(token);
            _students.Update(student);
            await _students.SaveChangesAsync();
            return token;
        }

        public async Task<string?> FeedAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return null;
            var student = await _students.GetByCalendarTokenHashAsync(Hash(token));
            if (student == null) return null;

            var today = _time.GetLocalNow().Date;
            var entries = (await ForUserAsync(Roles.Student, student.StudentId, today.AddDays(-30), today.AddDays(FeedDaysAhead)))
                .Where(e => e.Kind != CalendarKind.Class || e.Start >= today.AddDays(-FeedDaysBack))
                .ToList();
            return IcsCalendar.Write("ISMLTS classes and due dates", entries, _time.GetUtcNow().UtcDateTime);
        }

        public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    // Sends each note's reminder to the bell once its date (and time, or 07:00) has come
    public interface INoteReminderSender
    {
        Task<int> SendDueAsync(DateTime localNow);
    }

    public class NoteReminderSender : INoteReminderSender
    {
        public static readonly TimeOnly DefaultTime = new(7, 0);

        private readonly ICalendarNoteRepository _notes;
        private readonly INotificationService _notifications;

        public NoteReminderSender(ICalendarNoteRepository notes, INotificationService notifications)
        {
            _notes = notes;
            _notifications = notifications;
        }

        public async Task<int> SendDueAsync(DateTime localNow)
        {
            var due = (await _notes.GetUnsentRemindersAsync(localNow.Date))
                .Where(n => n.Date.Date + (n.Time ?? DefaultTime).ToTimeSpan() <= localNow)
                .ToList();
            foreach (var note in due)
            {
                var when = note.Time is TimeOnly t ? $" at {t.ToString("HH:mm", CultureInfo.InvariantCulture)}" : string.Empty;
                await _notifications.NotifyAsync([new Recipient(note.Role, note.UserId)], $"Reminder: {note.Title}",
                    $"{note.Date:ddd, dd MMM}{when}{(note.Details != null ? " · " + note.Details : string.Empty)}",
                    string.Create(CultureInfo.InvariantCulture, $"/Calendar?month={note.Date:yyyy-MM}"));
                note.RemindedAt = DateTime.UtcNow;
            }
            if (due.Count > 0) await _notes.SaveChangesAsync();
            return due.Count;
        }
    }

    public class NoteReminderService : BackgroundService
    {
        private static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<NoteReminderService> _logger;

        public NoteReminderService(IServiceScopeFactory scopes, ILogger<NoteReminderService> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(CheckEvery);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<INoteReminderSender>().SendDueAsync(DateTime.Now);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogWarning(e, "Sending note reminders failed; trying again on the next check");
                }
            }
        }
    }
}
