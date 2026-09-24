using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.ViewComponents
{
    public class NotificationBellViewComponent : ViewComponent
    {
        private readonly IAssessmentRepository _assessmentRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly IStudentRepository _studentRepository;

        public NotificationBellViewComponent(
            IAssessmentRepository assessmentRepository,
            IModuleRepository moduleRepository,
            IStudentRepository studentRepository)
        {
            _assessmentRepository = assessmentRepository;
            _moduleRepository = moduleRepository;
            _studentRepository = studentRepository;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var idClaim = UserClaimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var items = new List<string>();

            if (idClaim != null && int.TryParse(idClaim, out var userId))
            {
                if (UserClaimsPrincipal.IsInRole("Lecturer"))
                {
                    var myModuleIds = (await _moduleRepository.GetByLecturerAsync(userId)).Select(m => m.ModuleId).ToHashSet();
                    items = (await _assessmentRepository.GetUpcomingAsync(20))
                        .Where(a => myModuleIds.Contains(a.ModuleId)).Take(5)
                        .Select(a => $"{a.Name} due {a.DueDate:ddd, dd MMM}").ToList();
                }
                else if (UserClaimsPrincipal.IsInRole("Student"))
                {
                    var student = await _studentRepository.GetByIdWithModulesAsync(userId);
                    var myModuleIds = student?.Modules.Select(m => m.ModuleId).ToHashSet() ?? new HashSet<int>();
                    items = (await _assessmentRepository.GetUpcomingAsync(20))
                        .Where(a => myModuleIds.Contains(a.ModuleId)).Take(5)
                        .Select(a => $"{a.Name} due {a.DueDate:ddd, dd MMM}").ToList();
                }
            }

            return View(items);
        }
    }
}