using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Controllers
{
    public class CoursesController : Controller
    {
        private readonly ICourseRepository _courseRepository;

        public CoursesController(ICourseRepository courseRepository)
        {
            _courseRepository = courseRepository;
        }

        public async Task<IActionResult> Index() => View(await _courseRepository.GetAllAsync());

        public async Task<IActionResult> Details(int id)
        {
            var course = await _courseRepository.GetByIdWithModulesAsync(id);
            if (course == null) return NotFound();
            return View(course);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Code,Name")] Course course)
        {
            if (!ModelState.IsValid) return View(course);
            await _courseRepository.AddAsync(course);
            await _courseRepository.SaveChangesAsync();
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
            if (!ModelState.IsValid) return View(input);

            var course = await _courseRepository.GetByIdAsync(id);
            if (course == null) return NotFound();

            course.Code = input.Code;
            course.Name = input.Name;

            _courseRepository.Update(course);
            await _courseRepository.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            if (course == null) return NotFound();
            return View(course);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            if (course != null)
            {
                _courseRepository.Delete(course);
                await _courseRepository.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}