using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Controllers
{
    public class StudentsController : Controller
    {
        private readonly IStudentRepository _studentRepository;

        public StudentsController(IStudentRepository studentRepository)
        {
            _studentRepository = studentRepository;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _studentRepository.GetAllAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var student = await _studentRepository.GetByIdAsync(id);
            if (student == null) return NotFound();
            return View(student);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,Email,Programme")] Student student, string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                ModelState.AddModelError(string.Empty, "Password is required.");

            if (!ModelState.IsValid) return View(student);

            student.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            await _studentRepository.AddAsync(student);
            await _studentRepository.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var student = await _studentRepository.GetByIdAsync(id);
            if (student == null) return NotFound();
            return View(student);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("StudentId,FullName,Email,Programme")] Student input)
        {
            if (id != input.StudentId) return NotFound();
            if (!ModelState.IsValid) return View(input);

            var student = await _studentRepository.GetByIdAsync(id);
            if (student == null) return NotFound();

            student.FullName = input.FullName;
            student.Email = input.Email;
            student.Programme = input.Programme;

            _studentRepository.Update(student);
            await _studentRepository.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var student = await _studentRepository.GetByIdAsync(id);
            if (student == null) return NotFound();
            return View(student);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _studentRepository.GetByIdAsync(id);
            if (student != null)
            {
                _studentRepository.Delete(student);
                await _studentRepository.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}