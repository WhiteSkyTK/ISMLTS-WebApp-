using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    // Every signed-in role has notifications; each action only ever touches the caller's own (Role + UserId)
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notifications;
        private readonly INotificationSettingRepository _settings;
        private readonly IEmailSender _email;

        public NotificationsController(INotificationService notifications, INotificationSettingRepository settings, IEmailSender email)
        {
            _notifications = notifications;
            _settings = settings;
            _email = email;
        }

        public async Task<IActionResult> Index(string? filter, int page = 1)
        {
            if (User.GetUserId() is not int userId) return Forbid();

            var unreadOnly = filter == "unread";
            ViewBag.UnreadOnly = unreadOnly;
            ViewBag.UnreadCount = await _notifications.CountUnreadAsync(User.GetRole(), userId);
            return View(await _notifications.GetPageAsync(User.GetRole(), userId, unreadOnly, page));
        }

        // Marks it read, then follows its link (always a local path)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Open(int id)
        {
            if (User.GetUserId() is not int userId) return Forbid();

            var url = await _notifications.OpenAsync(User.GetRole(), userId, id);
            return url == null ? NotFound() : LocalRedirect(url);
        }

        // Called by site.js when the bell menu opens: the notifications just shown count as read
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(List<int> ids)
        {
            if (User.GetUserId() is not int userId) return Forbid();

            await _notifications.MarkReadAsync(User.GetRole(), userId, ids ?? new List<int>());
            return Json(new { unread = await _notifications.CountUnreadAsync(User.GetRole(), userId) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead(string? returnUrl)
        {
            if (User.GetUserId() is not int userId) return Forbid();

            var count = await _notifications.MarkReadAsync(User.GetRole(), userId, null);
            this.Toast(count == 0 ? "You're all caught up." : $"{count} notification(s) marked as read.", ToastTypes.Info);
            return LocalUrl.IsLocal(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Student,Lecturer")]
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            if (User.GetUserId() is not int userId) return Forbid();

            ViewBag.EmailEnabled = _email.IsEnabled;
            var settings = await _settings.GetAsync(User.GetRole(), userId)
                ?? new NotificationSetting { Role = User.GetRole(), UserId = userId };
            return View(settings);
        }

        [Authorize(Roles = "Student,Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings([Bind("EmailEnabled,WeeklyDigest")] NotificationSetting input)
        {
            if (User.GetUserId() is not int userId) return Forbid();

            var settings = await _settings.GetAsync(User.GetRole(), userId);
            if (settings == null)
            {
                settings = new NotificationSetting { Role = User.GetRole(), UserId = userId };
                await _settings.AddAsync(settings);
            }
            settings.EmailEnabled = input.EmailEnabled;
            if (User.IsInRole(Roles.Student))
            {
                settings.WeeklyDigest = input.WeeklyDigest;
            }

            await _settings.SaveChangesAsync();
            this.Toast("Your notification settings were saved.");
            return RedirectToAction(nameof(Settings));
        }
    }
}
