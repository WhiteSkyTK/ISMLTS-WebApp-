using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CoursesController : Controller
    {
        private const string CodeTakenMessage = "Another course already uses that code.";

        private readonly ICourseRepository _courseRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly ICourseService _courseService;

        public CoursesController(
            ICourseRepository courseRepository,
            IModuleRepository moduleRepository,
            IStudentRepository studentRepository,
            ICourseService courseService)
        {
            _courseRepository = courseRepository;
            _moduleRepository = moduleRepository;
            _studentRepository = studentRepository;
            _courseService = courseService;
        }

        public async Task<IActionResult> Index(string? q, int page = 1) =>
            View(await _courseRepository.SearchAsync(q, page));

        public async Task<IActionResult> Details(int id)
        {
            var course = await _courseRepository.GetByIdWithModulesAsync(id);
            if (course == null) return NotFound();
            return View(course);
        }

        [HttpGet]
        public async Task<IActionResult> Modules(int id)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            if (course == null) return NotFound();

            var modules = await _moduleRepository.GetAllWithCourseAsync();
            return View(new CourseModulesViewModel
            {
                CourseId = course.CourseId,
                CourseDisplay = $"{course.Code} - {course.Name}",
                Modules = modules.Select(m => new CourseModuleRow
                {
                    ModuleId = m.ModuleId,
                    Code = m.Code,
                    Name = m.Name,
                    Term = m.Term,
                    IsInCourse = m.CourseId == id,
                    OtherCourseCode = m.CourseId != null && m.CourseId != id ? m.Course?.Code : null
                }).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Modules(int id, List<int> moduleIds)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            if (course == null) return NotFound();

            var count = await _courseService.AssignModulesAsync(id, moduleIds ?? new List<int>());
            this.Toast($"{course.Code} now has {count} module(s). Enrol students to put them in all of them at once.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Enrol(int id)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            if (course == null) return NotFound();

            var modules = await _moduleRepository.GetByCourseWithStudentsAsync(id);
            var enrolledCounts = modules.SelectMany(m => m.Students)
                .GroupBy(s => s.StudentId)
                .ToDictionary(g => g.Key, g => g.Count());

            return View(new CourseEnrolmentViewModel
            {
                CourseId = course.CourseId,
                CourseDisplay = $"{course.Code} - {course.Name}",
                Term1Modules = modules.Count(m => m.Term == CourseTerms.Term1),
                Term2Modules = modules.Count(m => m.Term == CourseTerms.Term2),
                Students = (await _studentRepository.GetAllAsync()).OrderBy(s => s.FullName).Select(s => new CourseStudentRow
                {
                    StudentId = s.StudentId,
                    FullName = s.FullName,
                    Email = s.Email,
                    Programme = s.Programme,
                    EnrolledModules = enrolledCounts.GetValueOrDefault(s.StudentId)
                }).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enrol(int id, List<int> selectedStudentIds, string term, string mode)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            if (course == null) return NotFound();
            if (!CourseTerms.IsValid(term)) return BadRequest();

            if (selectedStudentIds == null || selectedStudentIds.Count == 0)
            {
                this.Toast("Tick at least one student first.", ToastTypes.Info);
                return RedirectToAction(nameof(Enrol), new { id });
            }

            var termText = term == CourseTerms.All ? "" : term == CourseTerms.Term1 ? "Term 1 " : "Term 2 ";
            if (mode == "remove")
            {
                var removed = await _courseService.UnenrolAsync(course, selectedStudentIds, term);
                this.Toast($"Removed {removed.Students} student(s) from {removed.Modules} {termText}module(s) in {course.Code}.", ToastTypes.Info);
            }
            else
            {
                var added = await _courseService.EnrolAsync(course, selectedStudentIds, term);
                this.Toast($"Enrolled {added.Students} student(s) in {added.Modules} {termText}module(s) of {course.Code} ({added.Changes} new enrolments).");
            }
            return RedirectToAction(nameof(Enrol), new { id });
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Code,Name")] Course course)
        {
            await CheckCodeIsFreeAsync(course.Code, 0);
            if (!ModelState.IsValid) return View(course);
            await _courseRepository.AddAsync(course);
            if (!await TrySaveAsync(course.Code, 0)) return View(course);

            this.Toast($"{course.Code} was added.");
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            if (course == null) return NotFound();
            return View(course);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CourseId,Code,Name")] Course input)
        {
            if (id != input.CourseId) return NotFound();
            await CheckCodeIsFreeAsync(input.Code, id);
            if (!ModelState.IsValid) return View(input);

            var course = await _courseRepository.GetByIdAsync(id);
            if (course == null) return NotFound();

            course.Code = input.Code;
            course.Name = input.Name;

            _courseRepository.Update(course);
            if (!await TrySaveAsync(input.Code, id)) return View(input);

            this.Toast($"Changes to {course.Code} were saved.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            if (course == null) return NotFound();

            _courseRepository.Delete(course);
            await _courseRepository.SaveChangesAsync();
            this.Toast($"{course.Code} was deleted. Its modules are now unassigned.");
            return RedirectToAction(nameof(Index));
        }

        private async Task CheckCodeIsFreeAsync(string code, int courseId)
        {
            if (!string.IsNullOrWhiteSpace(code) && await _courseRepository.CodeExistsAsync(code, courseId))
                ModelState.AddModelError(nameof(Course.Code), CodeTakenMessage);
        }

        // Two saves racing past the check above are stopped by the unique index
        private async Task<bool> TrySaveAsync(string code, int courseId)
        {
            try
            {
                await _courseRepository.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                if (!await _courseRepository.CodeExistsAsync(code, courseId)) throw;
                ModelState.AddModelError(nameof(Course.Code), CodeTakenMessage);
                return false;
            }
        }
    }
}
