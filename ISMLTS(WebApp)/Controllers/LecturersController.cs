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
    public class LecturersController : Controller
    {
        private const string EmailTakenMessage = "That email already belongs to another lecturer or student.";

        private readonly ILecturerRepository _lecturerRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IModuleRepository _moduleRepository;

        private readonly IAuditLog _audit;

        public LecturersController(ILecturerRepository lecturerRepository, IStudentRepository studentRepository, IModuleRepository moduleRepository, IAuditLog audit)
        {
            _audit = audit;
            _lecturerRepository = lecturerRepository;
            _studentRepository = studentRepository;
            _moduleRepository = moduleRepository;
        }

        public async Task<IActionResult> Index(string? q, int page = 1, string? sort = null) =>
            View(await _lecturerRepository.SearchAsync(q, page, sort));

        public async Task<IActionResult> Details(int id)
        {
            var lecturer = await _lecturerRepository.GetByIdAsync(id);
            if (lecturer == null) return NotFound();
            ViewBag.Modules = (await _moduleRepository.GetByLecturerAsync(id)).OrderBy(m => m.Code).ToList();
            return View(lecturer);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,Email")] Lecturer lecturer, string password)
        {
            if (!PasswordRules.IsLongEnough(password))
                ModelState.AddModelError(string.Empty, PasswordRules.TooShortMessage);
            await CheckEmailIsFreeAsync(lecturer.Email, 0);

            if (!ModelState.IsValid) return View(lecturer);

            lecturer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            await _lecturerRepository.AddAsync(lecturer);
            if (!await TrySaveAsync(lecturer.Email, 0)) return View(lecturer);

            this.Toast($"{lecturer.FullName} was added.");
            await _audit.RecordAsync(User, AuditActions.AccountCreated, $"Lecturer {lecturer.FullName} ({lecturer.Email})");
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
            await CheckEmailIsFreeAsync(input.Email, id);
            if (!ModelState.IsValid) return View(input);

            var lecturer = await _lecturerRepository.GetByIdAsync(id);
            if (lecturer == null) return NotFound();

            lecturer.FullName = input.FullName;
            lecturer.Email = input.Email;

            _lecturerRepository.Update(lecturer);
            if (!await TrySaveAsync(input.Email, id)) return View(input);

            this.Toast($"Changes to {lecturer.FullName} were saved.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var lecturer = await _lecturerRepository.GetByIdAsync(id);
            if (lecturer == null) return NotFound();

            // Modules can't be left without a lecturer (the foreign key is Restrict)
            if ((await _moduleRepository.GetByLecturerAsync(id)).Any())
            {
                this.Toast($"{lecturer.FullName} still teaches modules. Give them to another lecturer under Modules first.", ToastTypes.Danger);
                return RedirectToAction(nameof(Index));
            }

            _lecturerRepository.Delete(lecturer);
            await _lecturerRepository.SaveChangesAsync();
            this.Toast($"{lecturer.FullName} was deleted.");
            await _audit.RecordAsync(User, AuditActions.AccountDeleted, $"Lecturer {lecturer.FullName} ({lecturer.Email})");
            return RedirectToAction(nameof(Index));
        }

        // Login looks up lecturers and students by email, so an email must be unique across both
        private async Task CheckEmailIsFreeAsync(string email, int lecturerId)
        {
            if (string.IsNullOrWhiteSpace(email)) return;
            if (await _lecturerRepository.EmailExistsAsync(email, lecturerId) || await _studentRepository.EmailExistsAsync(email))
                ModelState.AddModelError(nameof(Lecturer.Email), EmailTakenMessage);
        }

        // Two saves racing past the check above are stopped by the unique index
        private async Task<bool> TrySaveAsync(string email, int lecturerId)
        {
            try
            {
                await _lecturerRepository.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                if (!await _lecturerRepository.EmailExistsAsync(email, lecturerId)) throw;
                ModelState.AddModelError(nameof(Lecturer.Email), EmailTakenMessage);
                return false;
            }
        }
    }
}
