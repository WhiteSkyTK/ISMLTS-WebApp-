using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IStudentRepository _studentRepository;
        private readonly ILecturerRepository _lecturerRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly IAssessmentRepository _assessmentRepository;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            IStudentRepository studentRepository,
            ILecturerRepository lecturerRepository,
            IModuleRepository moduleRepository,
            IAssessmentRepository assessmentRepository,
            ILogger<HomeController> logger)
        {
            _studentRepository = studentRepository;
            _lecturerRepository = lecturerRepository;
            _moduleRepository = moduleRepository;
            _assessmentRepository = assessmentRepository;
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

                model.Courses = student?.Modules.Select(m => new CourseCard
                {
                    ModuleId = m.ModuleId,
                    Code = m.Code,
                    Name = m.Name,
                    IsCurrentSemester = true,
                    Rating = 4.3,
                    DiscussionCount = 6
                }).ToList() ?? new List<CourseCard>();
            }

            // Placeholder until an Announcement model exists
            model.Announcements = new List<string>
            {
                "POE submission window opens Monday",
                "Quiz 2 covers weeks 3-5"
            };

            if (User.IsInRole("Lecturer"))
            {
                var myModuleIds = (await _moduleRepository.GetByLecturerAsync(userId)).Select(m => m.ModuleId).ToHashSet();
                model.UpcomingTasks = (await _assessmentRepository.GetUpcomingAsync(20))
                    .Where(a => myModuleIds.Contains(a.ModuleId))
                    .Take(5)
                    .Select(a => new UpcomingTask { Title = a.Name, Type = a.Type, DueDate = a.DueDate })
                    .ToList();
            }

            if (User.IsInRole("Student"))
            {
                var myModuleIds = student?.Modules.Select(m => m.ModuleId).ToHashSet() ?? new HashSet<int>();
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
    }
}