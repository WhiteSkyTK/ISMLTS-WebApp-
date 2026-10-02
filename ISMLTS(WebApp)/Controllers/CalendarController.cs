using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    // The next four weeks of classes, due dates and term dates for whoever is signed in,
    // and the students' private feed link for phone calendars
    [Authorize]
    public class CalendarController : Controller
    {
        public const int Days = 28;

        private readonly ICalendarService _calendar;

        public CalendarController(ICalendarService calendar)
        {
            _calendar = calendar;
        }

        public async Task<IActionResult> Index() => View(await PageAsync(null));

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
            return View(nameof(Index), await PageAsync(Url.Action(nameof(Feed), "Calendar", new { token }, Request.Scheme)));
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

        private async Task<CalendarPageModel> PageAsync(string? feedUrl)
        {
            var from = DateTime.Today;
            var to = from.AddDays(Days);
            var userId = User.GetUserId() ?? 0;
            var entries = User.IsInRole(Roles.Student) ? await _calendar.ForStudentAsync(userId, from, to)
                : User.IsInRole(Roles.Lecturer) ? await _calendar.ForLecturerAsync(userId, from, to)
                : await _calendar.ForAdminAsync(from, to);
            return new CalendarPageModel { Entries = entries, From = from, FeedUrl = feedUrl };
        }
    }
}
