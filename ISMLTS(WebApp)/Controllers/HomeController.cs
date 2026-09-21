using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Controllers
{
    public class HomeController : Controller
    {
        private readonly IStudentRepository _studentRepository;
        private readonly ILecturerRepository _lecturerRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            IStudentRepository studentRepository,
            ILecturerRepository lecturerRepository,
            IModuleRepository moduleRepository,
            ILogger<HomeController> logger)
        {
            _studentRepository = studentRepository;
            _lecturerRepository = lecturerRepository;
            _moduleRepository = moduleRepository;
            _logger = logger;
        }

        //[Authorize]
        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel();

            if (User.IsInRole("Admin"))
            {
                model.TotalStudents = (await _studentRepository.GetAllAsync()).Count();
                model.TotalLecturers = (await _lecturerRepository.GetAllAsync()).Count();
                model.TotalModules = (await _moduleRepository.GetAllAsync()).Count();
            }

            if (User.IsInRole("Student"))
            {
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var student = idClaim != null && int.TryParse(idClaim, out var sid)
                    ? await _studentRepository.GetByIdWithModulesAsync(sid)
                    : null;

                // Rating/DiscussionCount are placeholders until Review/Discussion models exist
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

            // Placeholder until Announcement/ICE/Quiz/POE models exist
            model.Announcements = new List<string>
            {
                "POE submission window opens Monday",
                "Quiz 2 covers weeks 3-5"
            };
            model.UpcomingTasks = new List<UpcomingTask>
            {
                new() { Title = "ICE Task 3", Type = "ICE", DueDate = DateTime.Today.AddDays(2) },
                new() { Title = "Quiz 2", Type = "Quiz", DueDate = DateTime.Today.AddDays(4) },
                new() { Title = "POE Part 1", Type = "POE", DueDate = DateTime.Today.AddDays(9) },
            };

            return View(model);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}