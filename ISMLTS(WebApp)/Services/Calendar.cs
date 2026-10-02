using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    public enum CalendarKind { Class, Due, Term }

    // One thing on someone's calendar. Start/End are local times; all-day entries only use Start's date.
    public record CalendarEntry(string Uid, CalendarKind Kind, DateTime Start, DateTime? End, string Title, string? Location)
    {
        public bool AllDay => End == null;
    }

    public static class CalendarBuilder
    {
        // Classes from the timetable (only inside their module's term, once terms exist), due dates and term dates,
        // for the days from `from` up to (not including) `to`, in order
        public static List<CalendarEntry> Build(
            IReadOnlyCollection<Module> modules,
            IEnumerable<TimetableSlot> slots,
            IEnumerable<Assessment> assessments,
            IReadOnlyList<Term> terms,
            DateTime from,
            DateTime to)
        {
            var entries = new List<CalendarEntry>();
            var moduleById = modules.ToDictionary(m => m.ModuleId);
            var slotsByDay = slots.Where(s => moduleById.ContainsKey(s.ModuleId)).ToLookup(s => s.Day);

            for (var day = from.Date; day < to.Date; day = day.AddDays(1))
            {
                foreach (var slot in slotsByDay[day.DayOfWeek])
                {
                    var module = moduleById[slot.ModuleId];
                    var termed = terms.Any(t => t.Code == module.Term);
                    if (termed && Terms.Containing(terms, module.Term, day) == null) continue;
                    entries.Add(new CalendarEntry(
                        string.Create(CultureInfo.InvariantCulture, $"class-{slot.SlotId}-{day:yyyyMMdd}"),
                        CalendarKind.Class,
                        day + slot.StartTime.ToTimeSpan(),
                        day + slot.EndTime.ToTimeSpan(),
                        $"{module.Code} class",
                        slot.Venue));
                }
            }

            entries.AddRange(assessments
                .Where(a => moduleById.ContainsKey(a.ModuleId) && a.DueDate.Date >= from.Date && a.DueDate.Date < to.Date)
                .Select(a => new CalendarEntry(
                    string.Create(CultureInfo.InvariantCulture, $"due-{a.AssessmentId}"),
                    CalendarKind.Due, a.DueDate.Date, null, $"{moduleById[a.ModuleId].Code}: {a.Name} due", null)));

            foreach (var term in terms)
            {
                if (term.StartDate.Date >= from.Date && term.StartDate.Date < to.Date)
                    entries.Add(new CalendarEntry(string.Create(CultureInfo.InvariantCulture, $"term-{term.TermId}-start"), CalendarKind.Term, term.StartDate.Date, null, $"{term.Name} starts", null));
                if (term.EndDate.Date >= from.Date && term.EndDate.Date < to.Date)
                    entries.Add(new CalendarEntry(string.Create(CultureInfo.InvariantCulture, $"term-{term.TermId}-end"), CalendarKind.Term, term.EndDate.Date, null, $"{term.Name} ends", null));
            }

            return entries.OrderBy(e => e.Start.Date).ThenBy(e => e.AllDay ? 0 : 1).ThenBy(e => e.Start).ThenBy(e => e.Title).ToList();
        }
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
                    lines.Add("DTEND;VALUE=DATE:" + e.Start.AddDays(1).ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                }
                else
                {
                    // Local college time written as UTC, so phones in any time zone show the right moment
                    lines.Add("DTSTART:" + Utc(DateTime.SpecifyKind(e.Start, DateTimeKind.Local).ToUniversalTime()));
                    lines.Add("DTEND:" + Utc(DateTime.SpecifyKind(e.End!.Value, DateTimeKind.Local).ToUniversalTime()));
                }
                lines.Add("SUMMARY:" + Escape(e.Title));
                if (!string.IsNullOrEmpty(e.Location)) lines.Add("LOCATION:" + Escape(e.Location));
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
        Task<List<CalendarEntry>> ForStudentAsync(int studentId, DateTime from, DateTime to);
        Task<List<CalendarEntry>> ForLecturerAsync(int lecturerId, DateTime from, DateTime to);
        Task<List<CalendarEntry>> ForAdminAsync(DateTime from, DateTime to);

        // A new secret for the student's feed link; the old link stops working. Only its hash is kept.
        Task<string?> NewFeedTokenAsync(int studentId);
        Task<string?> FeedAsync(string? token);
    }

    public class CalendarService : ICalendarService
    {
        // The feed covers the last week and the next four months of classes, and every due date from a month back
        private const int FeedDaysBack = 7;
        private const int FeedDaysAhead = 120;

        private readonly IStudentRepository _students;
        private readonly IModuleRepository _modules;
        private readonly IAssessmentRepository _assessments;
        private readonly ITimetableRepository _timetable;
        private readonly ITermRepository _terms;
        private readonly TimeProvider _time;

        public CalendarService(
            IStudentRepository students,
            IModuleRepository modules,
            IAssessmentRepository assessments,
            ITimetableRepository timetable,
            ITermRepository terms,
            TimeProvider time)
        {
            _students = students;
            _modules = modules;
            _assessments = assessments;
            _timetable = timetable;
            _terms = terms;
            _time = time;
        }

        public async Task<List<CalendarEntry>> ForStudentAsync(int studentId, DateTime from, DateTime to)
        {
            var student = await _students.GetByIdWithModulesAsync(studentId);
            return student == null ? new List<CalendarEntry>() : await BuildAsync(student.Modules.ToList(), from, to);
        }

        public async Task<List<CalendarEntry>> ForLecturerAsync(int lecturerId, DateTime from, DateTime to) =>
            await BuildAsync((await _modules.GetByLecturerAsync(lecturerId)).ToList(), from, to);

        // Admins see the term dates and every module's due dates (classes would crowd it out)
        public async Task<List<CalendarEntry>> ForAdminAsync(DateTime from, DateTime to)
        {
            var modules = (await _modules.GetAllAsync()).ToList();
            return CalendarBuilder.Build(modules, [], await _assessments.GetByModulesAsync(modules.Select(m => m.ModuleId).ToList()),
                await _terms.GetOrderedAsync(), from, to);
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
            var entries = (await ForStudentAsync(student.StudentId, today.AddDays(-30), today.AddDays(FeedDaysAhead)))
                .Where(e => e.Kind != CalendarKind.Class || e.Start >= today.AddDays(-FeedDaysBack))
                .ToList();
            return IcsCalendar.Write("ISMLTS classes and due dates", entries, _time.GetUtcNow().UtcDateTime);
        }

        public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        private async Task<List<CalendarEntry>> BuildAsync(List<Module> modules, DateTime from, DateTime to)
        {
            var ids = modules.Select(m => m.ModuleId).ToList();
            return CalendarBuilder.Build(modules, await _timetable.GetByModulesAsync(ids), await _assessments.GetByModulesAsync(ids),
                await _terms.GetOrderedAsync(), from, to);
        }
    }
}
