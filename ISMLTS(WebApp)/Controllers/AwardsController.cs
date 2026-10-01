using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    [Authorize(Roles = Roles.Student)]
    public class AwardsController : Controller
    {
        private readonly IStudentRepository _studentRepository;
        private readonly IAssessmentRepository _assessmentRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IMarkRepository _markRepository;
        private readonly IAttendanceRepository _attendanceRepository;

        public AwardsController(
            IStudentRepository studentRepository,
            IAssessmentRepository assessmentRepository,
            ISubmissionRepository submissionRepository,
            IMarkRepository markRepository,
            IAttendanceRepository attendanceRepository)
        {
            _studentRepository = studentRepository;
            _assessmentRepository = assessmentRepository;
            _submissionRepository = submissionRepository;
            _markRepository = markRepository;
            _attendanceRepository = attendanceRepository;
        }

        public async Task<IActionResult> Index()
        {
            if (User.GetUserId() is not int studentId) return Forbid();
            var student = await _studentRepository.GetByIdWithModulesAsync(studentId);
            if (student == null) return NotFound();

            var moduleIds = student.Modules.Select(m => m.ModuleId).ToList();
            var attendedByModule = (await _attendanceRepository.GetRecordsByStudentAsync(studentId))
                .Where(r => r.Session != null)
                .GroupBy(r => r.Session!.ModuleId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.SessionId).Distinct().Count());

            var data = new StudentAwardData(
                studentId,
                student.Modules.ToList(),
                await _assessmentRepository.GetByModulesAsync(moduleIds),
                (await _submissionRepository.GetByStudentAsync(studentId)).ToList(),
                (await _markRepository.GetByModulesAsync(moduleIds)).ToList(),
                await _attendanceRepository.CountSessionsByModuleAsync(moduleIds),
                attendedByModule);

            return View(Awards.For(data, DateTime.Today));
        }
    }
}
