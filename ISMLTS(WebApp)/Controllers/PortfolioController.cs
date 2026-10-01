using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    // A student's own work: only ever their submissions and their released marks
    [Authorize(Roles = Roles.Student)]
    public class PortfolioController : Controller
    {
        private readonly IStudentRepository _studentRepository;
        private readonly IAssessmentRepository _assessmentRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IMarkRepository _markRepository;

        public PortfolioController(
            IStudentRepository studentRepository,
            IAssessmentRepository assessmentRepository,
            ISubmissionRepository submissionRepository,
            IMarkRepository markRepository)
        {
            _studentRepository = studentRepository;
            _assessmentRepository = assessmentRepository;
            _submissionRepository = submissionRepository;
            _markRepository = markRepository;
        }

        public async Task<IActionResult> Index()
        {
            if (User.GetUserId() is not int studentId) return Forbid();
            var student = await _studentRepository.GetByIdWithModulesAsync(studentId);
            if (student == null) return NotFound();

            var moduleIds = student.Modules.Select(m => m.ModuleId).ToList();
            var modules = Portfolio.Build(
                student.Modules,
                await _assessmentRepository.GetByModulesAsync(moduleIds),
                await _submissionRepository.GetByStudentAsync(studentId),
                (await _markRepository.GetByStudentAsync(studentId)).Where(m => moduleIds.Contains(m.ModuleId)));

            ViewBag.StudentName = student.FullName;
            return View(modules);
        }
    }
}
