using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    // My Progress for students, Class Insights for lecturers
    [Authorize]
    public class InsightsController : Controller
    {
        private readonly IStudentRepository _studentRepository;
        private readonly IMarkRepository _markRepository;
        private readonly IAttendanceRepository _attendanceRepository;
        private readonly IAssessmentRepository _assessmentRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly RiskOptions _risk;

        public InsightsController(
            IStudentRepository studentRepository,
            IMarkRepository markRepository,
            IAttendanceRepository attendanceRepository,
            IAssessmentRepository assessmentRepository,
            ISubmissionRepository submissionRepository,
            IOptions<RiskOptions> risk)
        {
            _studentRepository = studentRepository;
            _markRepository = markRepository;
            _attendanceRepository = attendanceRepository;
            _assessmentRepository = assessmentRepository;
            _submissionRepository = submissionRepository;
            _risk = risk.Value;
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyProgress()
        {
            if (User.GetUserId() is not int studentId) return Forbid();

            var student = await _studentRepository.GetByIdWithModulesAsync(studentId);
            if (student == null) return NotFound();

            var moduleIds = student.Modules.Select(m => m.ModuleId).ToList();
            var marks = (await _markRepository.GetByStudentAsync(studentId)).Where(m => m.IsVisibleToStudent).ToList();
            var attendedByModule = (await _attendanceRepository.GetRecordsByStudentAsync(studentId))
                .Where(r => r.Session != null)
                .GroupBy(r => r.Session!.ModuleId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.SessionId).Distinct().Count());
            var sessionsByModule = await _attendanceRepository.CountSessionsByModuleAsync(moduleIds);
            var assessments = await _assessmentRepository.GetByModulesAsync(moduleIds);
            var submitted = (await _submissionRepository.GetByStudentAsync(studentId))
                .Where(s => s.SubmittedAt != null)
                .Select(s => s.AssessmentId)
                .ToHashSet();

            var modules = student.Modules.OrderBy(m => m.Term).ThenBy(m => m.Code).Select(m => Progress.ForModule(
                new StudentModuleData(
                    m,
                    marks.Where(x => x.ModuleId == m.ModuleId).ToList(),
                    sessionsByModule.GetValueOrDefault(m.ModuleId),
                    attendedByModule.GetValueOrDefault(m.ModuleId),
                    assessments.Where(a => a.ModuleId == m.ModuleId).ToList(),
                    submitted),
                DateTime.Today,
                _risk.AttendanceThreshold)).ToList();

            return View(new MyProgressViewModel
            {
                Modules = modules,
                Summary = Progress.Summarise(modules),
                AttendanceThreshold = _risk.AttendanceThreshold
            });
        }
    }
}
