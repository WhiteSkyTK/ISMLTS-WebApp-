using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IStudentRepository _studentRepository;
        private readonly ILecturerRepository _lecturerRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly IAssessmentRepository _assessmentRepository;
        private readonly IAnnouncementRepository _announcementRepository;
        private readonly ITermService _terms;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            IStudentRepository studentRepository,
            ILecturerRepository lecturerRepository,
            IModuleRepository moduleRepository,
            IAssessmentRepository assessmentRepository,
            IAnnouncementRepository announcementRepository,
            ITermService terms,
            ILogger<HomeController> logger)
        {
            _studentRepository = studentRepository;
            _lecturerRepository = lecturerRepository;
            _moduleRepository = moduleRepository;
            _assessmentRepository = assessmentRepository;
            _announcementRepository = announcementRepository;
            _terms = terms;
            _logger = logger;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated != true) return View("Landing");

            var model = new DashboardViewModel();

            var userId = User.GetUserId() ?? 0;

            if (User.IsInRole("Admin"))
            {
                model.TotalStudents = (await _studentRepository.GetAllAsync()).Count();
                model.TotalLecturers = (await _lecturerRepository.GetAllAsync()).Count();
                model.TotalModules = (await _moduleRepository.GetAllAsync()).Count();
            }

            Student? student = null;
            if (User.IsInRole("Student"))
            {
                student = await _studentRepository.GetByIdWithModulesAsync(userId);
                var currentTerm = await _terms.CurrentAsync();

                model.Courses = student?.Modules.Select(m => new CourseCard
                {
                    ModuleId = m.ModuleId,
                    Code = m.Code,
                    Name = m.Name,
                    IsCurrentSemester = Terms.IsCurrent(m.Term, currentTerm)
                }).ToList() ?? new List<CourseCard>();
            }

            if (User.IsInRole("Admin"))
            {
                model.Announcements = await _announcementRepository.GetLatestAsync(3);
            }

            if (User.IsInRole("Lecturer"))
            {
                var myModuleIds = (await _moduleRepository.GetByLecturerAsync(userId)).Select(m => m.ModuleId).ToHashSet();
                model.Announcements = await _announcementRepository.GetForLecturerAsync(myModuleIds, 3);
                model.UpcomingTasks = (await _assessmentRepository.GetUpcomingAsync(20))
                    .Where(a => myModuleIds.Contains(a.ModuleId))
                    .Take(5)
                    .Select(a => new UpcomingTask { Title = a.Name, Type = a.Type, DueDate = a.DueDate })
                    .ToList();
            }

            if (User.IsInRole("Student"))
            {
                var myModuleIds = student?.Modules.Select(m => m.ModuleId).ToHashSet() ?? new HashSet<int>();
                model.Announcements = await _announcementRepository.GetForStudentAsync(myModuleIds, 5);
                model.UpcomingTasks = (await _assessmentRepository.GetUpcomingAsync(20))
                    .Where(a => myModuleIds.Contains(a.ModuleId))
                    .Take(5)
                    .Select(a => new UpcomingTask { Title = a.Name, Type = a.Type, DueDate = a.DueDate })
                    .ToList();
            }

            return View(model);
        }

        [AllowAnonymous]
        public IActionResult Privacy() => View();

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

        // UseStatusCodePagesWithReExecute sends empty 4xx/5xx responses here and keeps the original status code
        [AllowAnonymous]
        [Route("/Status/{code:int}")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Status(int code)
        {
            var page = StatusPages.Describe(code);
            Response.StatusCode = page.Code;
            return View(page);
        }
    }
}