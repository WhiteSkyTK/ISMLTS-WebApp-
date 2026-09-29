using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CoursesController : Controller
    {
        private const string CodeTakenMessage = "Another course already uses that code.";

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
            await CheckCodeIsFreeAsync(course.Code, 0);
            if (!ModelState.IsValid) return View(course);
            await _courseRepository.AddAsync(course);
            return await TrySaveAsync(course.Code, 0) ? RedirectToAction(nameof(Index)) : View(course);
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
            return await TrySaveAsync(input.Code, id) ? RedirectToAction(nameof(Index)) : View(input);
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
