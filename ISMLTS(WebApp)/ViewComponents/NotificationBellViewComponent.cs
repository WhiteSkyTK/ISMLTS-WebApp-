using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.ViewComponents
{
    public class NotificationBellViewComponent : ViewComponent
    {
        private const int LatestCount = 8;

        private readonly INotificationService _notifications;
        private readonly IAssessmentRepository _assessmentRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly IStudentRepository _studentRepository;

        public NotificationBellViewComponent(
            INotificationService notifications,
            IAssessmentRepository assessmentRepository,
            IModuleRepository moduleRepository,
            IStudentRepository studentRepository)
        {
            _notifications = notifications;
            _assessmentRepository = assessmentRepository;
            _moduleRepository = moduleRepository;
            _studentRepository = studentRepository;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new NotificationBellViewModel();
            if (UserClaimsPrincipal.GetUserId() is not int userId)
                return View(model);

            var role = UserClaimsPrincipal.GetRole();
            model.UnreadCount = await _notifications.CountUnreadAsync(role, userId);
            model.Latest = await _notifications.GetLatestAsync(role, userId, LatestCount);
            model.DueSoon = await DueSoonAsync(role, userId);
            return View(model);
        }

        // The "due soon" list from before notifications existed stays as a second section
        private async Task<List<string>> DueSoonAsync(string role, int userId)
        {
            HashSet<int> myModuleIds;
            if (role == Roles.Lecturer)
            {
                myModuleIds = (await _moduleRepository.GetByLecturerAsync(userId)).Select(m => m.ModuleId).ToHashSet();
            }
            else if (role == Roles.Student)
            {
                var student = await _studentRepository.GetByIdWithModulesAsync(userId);
                myModuleIds = student?.Modules.Select(m => m.ModuleId).ToHashSet() ?? new HashSet<int>();
            }
            else
            {
                return new List<string>();
            }

            return (await _assessmentRepository.GetUpcomingAsync(20))
                .Where(a => myModuleIds.Contains(a.ModuleId)).Take(5)
                .Select(a => $"{a.Name} ({a.Module?.Code}) · {DueDates.Describe(a.DueDate, DateTime.Today)}")
                .ToList();
        }
    }
}
