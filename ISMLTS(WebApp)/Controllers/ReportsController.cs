using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    // Site-wide numbers for admins: users, and attendance and risk per module
    [Authorize(Roles = Roles.Admin)]
    public class ReportsController : Controller
    {
        private readonly IStudentRepository _studentRepository;
        private readonly ILecturerRepository _lecturerRepository;
        private readonly IAdminRepository _adminRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly IMarkRepository _markRepository;
        private readonly IAttendanceRepository _attendanceRepository;
        private readonly IAssessmentRepository _assessmentRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly RiskOptions _risk;

        public ReportsController(
            IStudentRepository studentRepository,
            ILecturerRepository lecturerRepository,
            IAdminRepository adminRepository,
            IModuleRepository moduleRepository,
            IMarkRepository markRepository,
            IAttendanceRepository attendanceRepository,
            IAssessmentRepository assessmentRepository,
            ISubmissionRepository submissionRepository,
            IOptions<RiskOptions> risk)
        {
            _studentRepository = studentRepository;
            _lecturerRepository = lecturerRepository;
            _adminRepository = adminRepository;
            _moduleRepository = moduleRepository;
            _markRepository = markRepository;
            _attendanceRepository = attendanceRepository;
            _assessmentRepository = assessmentRepository;
            _submissionRepository = submissionRepository;
            _risk = risk.Value;
        }

        public async Task<IActionResult> Index()
        {
            var modules = await _moduleRepository.GetAllWithDetailsAsync();
            ViewBag.Users = Reports.Users(
                await _studentRepository.GetAllWithModulesAsync(),
                (await _lecturerRepository.GetAllAsync()).ToList(),
                (await _adminRepository.GetAllAsync()).ToList(),
                modules);
            ViewBag.AttendanceThreshold = _risk.AttendanceThreshold;
            return View(await ModuleReportsAsync(modules));
        }

        public async Task<IActionResult> Export()
        {
            var csv = Reports.ModulesCsv(await ModuleReportsAsync(await _moduleRepository.GetAllWithDetailsAsync()));
            return File(Csv.ToUtf8WithBom(csv), "text/csv", $"module-report-{DateTime.Today:yyyy-MM-dd}.csv");
        }

        private async Task<List<ModuleReport>> ModuleReportsAsync(List<Module> modules)
        {
            var moduleIds = modules.Select(m => m.ModuleId).ToList();
            var marks = (await _markRepository.GetByModulesAsync(moduleIds)).ToList();
            var records = await _attendanceRepository.GetRecordsByModulesAsync(moduleIds);
            var sessions = await _attendanceRepository.CountSessionsByModuleAsync(moduleIds);
            var assessments = await _assessmentRepository.GetByModulesAsync(moduleIds);
            var submissions = await _submissionRepository.GetByAssessmentsAsync(assessments.Select(a => a.AssessmentId).ToList());

            return modules.Select(m => new ModuleReport(
                ClassInsights.ForModule(
                    new ClassModuleData(
                        m,
                        marks.Where(x => x.ModuleId == m.ModuleId).ToList(),
                        sessions.GetValueOrDefault(m.ModuleId),
                        records.Where(r => r.Session?.ModuleId == m.ModuleId).ToList(),
                        assessments.Where(a => a.ModuleId == m.ModuleId).ToList(),
                        submissions.Where(s => s.Assessment?.ModuleId == m.ModuleId).ToList()),
                    DateTime.Today,
                    _risk.AttendanceThreshold),
                m.Course?.Code ?? "None",
                m.Term == "Term2" ? "Term 2" : "Term 1",
                m.Lecturer?.FullName ?? "None",
                sessions.GetValueOrDefault(m.ModuleId))).ToList();
        }
    }
}
