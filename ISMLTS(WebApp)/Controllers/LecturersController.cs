using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Controllers
{
    public class LecturersController : Controller
    {
        private readonly ILecturerRepository _lecturerRepository;

        public LecturersController(ILecturerRepository lecturerRepository)
        {
            _lecturerRepository = lecturerRepository;
        }

        public async Task<IActionResult> Index() => View(await _lecturerRepository.GetAllAsync());

        public async Task<IActionResult> Details(int id)
        {
            var lecturer = await _lecturerRepository.GetByIdAsync(id);
            if (lecturer == null) return NotFound();
            return View(lecturer);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,Email")] Lecturer lecturer, string password)
        {
            if (!ModelState.IsValid) return View(lecturer);

            lecturer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            await _lecturerRepository.AddAsync(lecturer);
            await _lecturerRepository.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var lecturer = await _lecturerRepository.GetByIdAsync(id);
            if (lecturer == null) return NotFound();
            return View(lecturer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("LecturerId,FullName,Email")] Lecturer input)
        {
            if (id != input.LecturerId) return NotFound();
            if (!ModelState.IsValid) return View(input);

            var lecturer = await _lecturerRepository.GetByIdAsync(id);
            if (lecturer == null) return NotFound();

            lecturer.FullName = input.FullName;
            lecturer.Email = input.Email;

            _lecturerRepository.Update(lecturer);
            await _lecturerRepository.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var lecturer = await _lecturerRepository.GetByIdAsync(id);
            if (lecturer == null) return NotFound();
            return View(lecturer);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var lecturer = await _lecturerRepository.GetByIdAsync(id);
            if (lecturer != null)
            {
                _lecturerRepository.Delete(lecturer);
                await _lecturerRepository.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}