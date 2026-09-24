using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.ViewComponents
{
    public class ModuleSwitcherViewComponent : ViewComponent
    {
        private readonly IStudentRepository _studentRepository;

        public ModuleSwitcherViewComponent(IStudentRepository studentRepository)
        {
            _studentRepository = studentRepository;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var idClaim = UserClaimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (idClaim == null || !int.TryParse(idClaim, out var studentId))
                return View(new List<Module>());

            var student = await _studentRepository.GetByIdWithModulesAsync(studentId);
            return View(student?.Modules.ToList() ?? new List<Module>());
        }
    }
}