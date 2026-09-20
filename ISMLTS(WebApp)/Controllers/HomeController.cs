using System.Diagnostics;
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

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel();

            if (User.IsInRole("Admin"))
            {
                model.TotalStudents = (await _studentRepository.GetAllAsync()).Count();
                model.TotalLecturers = (await _lecturerRepository.GetAllAsync()).Count();
                model.TotalModules = (await _moduleRepository.GetAllAsync()).Count();
            }

            // Placeholder until Announcement/Task/Quiz models exist
            model.Announcements = new List<string>
            {
                "POE submission window opens Monday",
                "Quiz 2 covers weeks 3-5"
            };
            model.UpcomingDueDates = new List<string>
            {
                "ICE Task 3 - due Fri",
                "POE Part 1 - due next Wed"
            };

            return View(model);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}