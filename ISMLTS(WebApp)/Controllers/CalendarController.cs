using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    // A month of classes, due and closing dates, term and college dates and the person's own notes and to-dos,
    // plus the students' private feed link for phone calendars
    [Authorize]
    public class CalendarController : Controller
    {
        private readonly ICalendarService _calendar;

        public CalendarController(ICalendarService calendar)
        {
            _calendar = calendar;
        }

        public async Task<IActionResult> Index(string? month) => View(await PageAsync(month, null, null));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote([Bind("Date,Time,Title,Details,Remind")] CalendarNote note)
        {
            if (User.GetUserId() is not int userId || Role == null) return Forbid();
            ModelState.Clear(); // the service checks the note and names the field that's wrong
            if (await _calendar.AddNoteAsync(Role, userId, note) is { } problem)
            {
                ModelState.AddModelError(problem.Field, problem.Message);
                return View(nameof(Index), await PageAsync(MonthOf(note.Date), null, note));
            }
            this.Toast($"\"{note.Title}\" was added to {note.Date:ddd, dd MMM}{(note.Remind ? ". You'll get a reminder in the bell." : ".")}");
            return RedirectToAction(nameof(Index), new { month = MonthOf(note.Date) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetNoteDone(int id, bool done)
        {
            if (User.GetUserId() is not int userId || Role == null) return Forbid();
            var note = await _calendar.SetNoteDoneAsync(Role, userId, id, done);
            if (note == null) return NotFound();
            this.Toast(done ? $"\"{note.Title}\" is done." : $"\"{note.Title}\" is back on your list.", ToastTypes.Info);
            return RedirectToAction(nameof(Index), new { month = MonthOf(note.Date) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNote(int id)
        {
            if (User.GetUserId() is not int userId || Role == null) return Forbid();
            var note = await _calendar.DeleteNoteAsync(Role, userId, id);
            if (note == null) return NotFound();
            this.Toast($"\"{note.Title}\" was deleted.");
            return RedirectToAction(nameof(Index), new { month = MonthOf(note.Date) });
        }

        // Shows the new link once, on this response; asking again makes another and the old one stops working
        [Authorize(Roles = Roles.Student)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewFeedLink()
        {
            if (User.GetUserId() is not int studentId) return Forbid();
            var token = await _calendar.NewFeedTokenAsync(studentId);
            if (token == null) return NotFound();

            this.Toast("Your calendar link is ready. Copy it now: it is only shown once.");
            return View(nameof(Index), await PageAsync(null, Url.Action(nameof(Feed), "Calendar", new { token }, Request.Scheme), null));
        }

        // Phone calendars can't sign in, so the secret in the link is the key. The token goes in the query string,
        // which request logs and Application Insights leave out.
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Feed(string? token)
        {
            var ics = await _calendar.FeedAsync(token);
            if (ics == null) return NotFound();
            Response.Headers.CacheControl = "private, max-age=3600";
            return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", "ismlts.ics");
        }

        private string? Role => User.IsInRole(Roles.Admin) ? Roles.Admin : User.IsInRole(Roles.Lecturer) ? Roles.Lecturer : User.IsInRole(Roles.Student) ? Roles.Student : null;

        private static string MonthOf(DateTime day) => day.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        // Whole weeks (Monday to Sunday) around the month, so the grid has no gaps
        private async Task<CalendarPageModel> PageAsync(string? month, string? feedUrl, CalendarNote? form)
        {
            var first = DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) && parsed.Year is >= 2000 and <= 2100
                ? parsed
                : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var gridStart = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
            var last = first.AddMonths(1);
            var gridEnd = last.AddDays((7 - ((int)last.DayOfWeek + 6) % 7) % 7);

            return new CalendarPageModel
            {
                Month = first,
                GridStart = gridStart,
                GridEnd = gridEnd,
                Entries = await _calendar.ForUserAsync(Role ?? Roles.Student, User.GetUserId() ?? 0, gridStart, gridEnd),
                FeedUrl = feedUrl,
                NewNote = form ?? new CalendarNote { Date = first.Month == DateTime.Today.Month && first.Year == DateTime.Today.Year ? DateTime.Today : first }
            };
        }
    }
}
