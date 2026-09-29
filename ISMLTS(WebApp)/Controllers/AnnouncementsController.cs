using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    [Authorize]
    public class AnnouncementsController : Controller
    {
        private const int FeedSize = 50;
        private static readonly string[] AdminAudiences = { AnnouncementAudiences.Everyone, AnnouncementAudiences.Students, AnnouncementAudiences.Lecturers };

        private readonly IAnnouncementRepository _announcements;
        private readonly IModuleRepository _modules;
        private readonly IStudentRepository _students;
        private readonly INotificationService _notifications;

        public AnnouncementsController(
            IAnnouncementRepository announcements,
            IModuleRepository modules,
            IStudentRepository students,
            INotificationService notifications)
        {
            _announcements = announcements;
            _modules = modules;
            _students = students;
            _notifications = notifications;
        }

        // Everything the signed-in user is allowed to see, newest first
        public async Task<IActionResult> Index()
        {
            if (User.GetUserId() is not int userId) return Forbid();

            var role = User.GetRole();
            var feed = role switch
            {
                Roles.Student => await _announcements.GetForStudentAsync(await MyModuleIdsAsync(role, userId), FeedSize),
                Roles.Lecturer => await _announcements.GetForLecturerAsync(await MyModuleIdsAsync(role, userId), FeedSize),
                _ => await _announcements.GetLatestAsync(FeedSize)
            };
            return View(feed);
        }

        public async Task<IActionResult> Details(int id)
        {
            if (User.GetUserId() is not int userId) return Forbid();

            var announcement = await _announcements.GetByIdWithModuleAsync(id);
            if (announcement == null) return NotFound();

            var role = User.GetRole();
            return AnnouncementAudience.CanSee(announcement, role, await MyModuleIdsAsync(role, userId))
                ? View(announcement)
                : NotFound();
        }

        [Authorize(Roles = "Lecturer,Admin")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadFormDataAsync();
            return View(new AnnouncementForm());
        }

        [Authorize(Roles = "Lecturer,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Body,ModuleId,Audience")] AnnouncementForm form)
        {
            if (User.GetUserId() is not int userId) return Forbid();

            Module? module = null;
            var role = User.GetRole();
            if (role == Roles.Lecturer)
            {
                // Lecturers post to one of their own modules; its students are the audience
                module = form.ModuleId is int moduleId ? await _modules.GetByIdAsync(moduleId) : null;
                if (module == null || module.LecturerId != userId)
                {
                    ModelState.AddModelError(nameof(AnnouncementForm.ModuleId), "Pick one of your modules.");
                }
                form.Audience = AnnouncementAudiences.Students;
            }
            else if (!AdminAudiences.Contains(form.Audience))
            {
                ModelState.AddModelError(nameof(AnnouncementForm.Audience), "Pick who should see it.");
            }

            if (!ModelState.IsValid)
            {
                await LoadFormDataAsync();
                return View(form);
            }

            var announcement = new Announcement
            {
                Title = form.Title,
                Body = form.Body,
                ModuleId = module?.ModuleId,
                Audience = form.Audience,
                AuthorRole = role,
                AuthorId = userId,
                AuthorName = User.Identity?.Name ?? role,
                CreatedAt = DateTime.UtcNow
            };
            await _announcements.AddAsync(announcement);
            await _announcements.SaveChangesAsync();
            await _notifications.AnnouncementPostedAsync(announcement, module);

            this.Toast(module != null ? $"Posted to {module.Code}. Its students have been notified." : "Posted. Everyone in the audience has been notified.");
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Lecturer,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var announcement = await _announcements.GetByIdAsync(id);
            if (announcement == null || announcement.AuthorRole != User.GetRole() || announcement.AuthorId != User.GetUserId())
                return NotFound();

            _announcements.Delete(announcement);
            await _announcements.SaveChangesAsync();
            this.Toast($"\"{announcement.Title}\" was deleted.");
            return RedirectToAction(nameof(Index));
        }

        // Students: the modules they're enrolled in. Lecturers: the modules they teach.
        private async Task<IReadOnlyCollection<int>> MyModuleIdsAsync(string role, int userId)
        {
            if (role == Roles.Lecturer)
                return (await _modules.GetByLecturerAsync(userId)).Select(m => m.ModuleId).ToList();
            if (role == Roles.Student)
                return (await _students.GetByIdWithModulesAsync(userId))?.Modules.Select(m => m.ModuleId).ToList() ?? new List<int>();
            return Array.Empty<int>();
        }

        private async Task LoadFormDataAsync()
        {
            if (User.IsInRole(Roles.Lecturer))
            {
                ViewBag.Modules = (await _modules.GetByLecturerAsync(User.GetUserId() ?? 0))
                    .OrderBy(m => m.Code)
                    .Select(m => new { m.ModuleId, Display = $"{m.Code} - {m.Name}" })
                    .ToList();
            }
        }
    }
}
