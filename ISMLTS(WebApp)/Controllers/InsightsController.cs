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
        private readonly IMarkRepository _markRepository;
        private readonly IAttendanceRepository _attendanceRepository;
        private readonly ITermService _terms;
        private readonly IAssessmentRepository _assessmentRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly IStudentPortalService _portal;
        private readonly RiskOptions _risk;

        public InsightsController(
            IMarkRepository markRepository,
            IAttendanceRepository attendanceRepository,
            ITermService terms,
            IAssessmentRepository assessmentRepository,
            ISubmissionRepository submissionRepository,
            IModuleRepository moduleRepository,
            IStudentPortalService portal,
            IOptions<RiskOptions> risk)
        {
            _portal = portal;
            _markRepository = markRepository;
            _attendanceRepository = attendanceRepository;
            _terms = terms;
            _assessmentRepository = assessmentRepository;
            _submissionRepository = submissionRepository;
            _moduleRepository = moduleRepository;
            _risk = risk.Value;
        }

        // One panel per module the lecturer teaches, with who is at risk and why
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Class()
        {
            if (User.GetUserId() is not int lecturerId) return Forbid();

            var modules = new List<Module>();
            foreach (var owned in (await _moduleRepository.GetByLecturerAsync(lecturerId)).OrderBy(m => m.Term).ThenBy(m => m.Code))
            {
                var withStudents = await _moduleRepository.GetByIdWithDetailsAsync(owned.ModuleId);
                if (withStudents != null) modules.Add(withStudents);
            }

            var moduleIds = modules.Select(m => m.ModuleId).ToList();
            var marks = (await _markRepository.GetByModulesAsync(moduleIds)).ToList();
            var periods = await _terms.AttendancePeriodsAsync();
            var records = await _attendanceRepository.GetRecordsByModulesAsync(moduleIds, periods);
            var sessions = await _attendanceRepository.CountSessionsByModuleAsync(moduleIds, periods);
            var assessments = await _assessmentRepository.GetByModulesAsync(moduleIds);
            var submissions = await _submissionRepository.GetByAssessmentsAsync(assessments.Select(a => a.AssessmentId).ToList());

            return View(new ClassInsightsViewModel
            {
                AttendanceThreshold = _risk.AttendanceThreshold,
                Modules = modules.Select(m => ClassInsights.ForModule(
                    new ClassModuleData(
                        m,
                        marks.Where(x => x.ModuleId == m.ModuleId).ToList(),
                        sessions.GetValueOrDefault(m.ModuleId),
                        records.Where(r => r.Session?.ModuleId == m.ModuleId).ToList(),
                        assessments.Where(a => a.ModuleId == m.ModuleId).ToList(),
                        submissions.Where(s => s.Assessment?.ModuleId == m.ModuleId).ToList()),
                    DateTime.Today,
                    _risk.AttendanceThreshold)).ToList()
            });
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyProgress()
        {
            if (User.GetUserId() is not int studentId) return Forbid();

            var progress = await _portal.ProgressAsync(studentId);
            return progress == null ? NotFound() : View(progress);
        }
    }
}
