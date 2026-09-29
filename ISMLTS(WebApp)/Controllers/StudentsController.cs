using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    [Authorize(Roles = "Admin")]
    public class StudentsController : Controller
    {
        private const string EmailTakenMessage = "That email already belongs to another student or lecturer.";

        private readonly IStudentRepository _studentRepository;
        private readonly ILecturerRepository _lecturerRepository;

        public StudentsController(IStudentRepository studentRepository, ILecturerRepository lecturerRepository)
        {
            _studentRepository = studentRepository;
            _lecturerRepository = lecturerRepository;
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
            if (!PasswordRules.IsLongEnough(password))
                ModelState.AddModelError(string.Empty, PasswordRules.TooShortMessage);
            await CheckEmailIsFreeAsync(student.Email, 0);

            if (!ModelState.IsValid) return View(student);

            student.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            await _studentRepository.AddAsync(student);
            return await TrySaveAsync(student.Email, 0) ? RedirectToAction(nameof(Index)) : View(student);
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
            await CheckEmailIsFreeAsync(input.Email, id);
            if (!ModelState.IsValid) return View(input);

            var student = await _studentRepository.GetByIdAsync(id);
            if (student == null) return NotFound();

            student.FullName = input.FullName;
            student.Email = input.Email;
            student.Programme = input.Programme;

            _studentRepository.Update(student);
            return await TrySaveAsync(input.Email, id) ? RedirectToAction(nameof(Index)) : View(input);
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

        // Login looks up lecturers and students by email, so an email must be unique across both
        private async Task CheckEmailIsFreeAsync(string email, int studentId)
        {
            if (string.IsNullOrWhiteSpace(email)) return;
            if (await _studentRepository.EmailExistsAsync(email, studentId) || await _lecturerRepository.EmailExistsAsync(email))
                ModelState.AddModelError(nameof(Student.Email), EmailTakenMessage);
        }

        // Two saves racing past the check above are stopped by the unique index
        private async Task<bool> TrySaveAsync(string email, int studentId)
        {
            try
            {
                await _studentRepository.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                if (!await _studentRepository.EmailExistsAsync(email, studentId)) throw;
                ModelState.AddModelError(nameof(Student.Email), EmailTakenMessage);
                return false;
            }
        }
    }
}