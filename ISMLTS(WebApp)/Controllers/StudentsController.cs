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
    public class StudentsController : Controller
    {
        private const string EmailTakenMessage = "That email already belongs to another student or lecturer.";

        private readonly IStudentRepository _studentRepository;
        private readonly ILecturerRepository _lecturerRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly ISubmissionFileService _files;
        private readonly StudentOptions _studentOptions;

        private readonly IAuditLog _audit;

        public StudentsController(
            IStudentRepository studentRepository,
            ILecturerRepository lecturerRepository,
            ICourseRepository courseRepository,
            ISubmissionFileService files,
            Microsoft.Extensions.Options.IOptions<StudentOptions> studentOptions, IAuditLog audit)
        {
            _audit = audit;
            _studentRepository = studentRepository;
            _lecturerRepository = lecturerRepository;
            _courseRepository = courseRepository;
            _files = files;
            _studentOptions = studentOptions.Value;
        }

        public async Task<IActionResult> Index(string? q, int page = 1, string? sort = null, string? programme = null)
        {
            ViewData["ListFilters"] = new List<ListFilter>
            {
                new()
                {
                    Name = "programme",
                    Label = "Programme",
                    AllText = "Every programme",
                    Selected = programme,
                    Options = (await _studentRepository.GetProgrammesAsync()).Select(p => (p, p))
                        .Append((ListFilters.None, "No programme set")).ToList()
                }
            };
            return View(await _studentRepository.SearchAsync(q, page, sort, programme));
        }

        public async Task<IActionResult> Details(int id)
        {
            var student = await _studentRepository.GetByIdWithModulesAsync(id);
            if (student == null) return NotFound();
            return View(student);
        }

        public async Task<IActionResult> Create()
        {
            await LoadProgrammesAsync(null);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,Email,Programme")] Student student, string password)
        {
            if (!PasswordRules.IsLongEnough(password))
                ModelState.AddModelError(string.Empty, PasswordRules.TooShortMessage);
            await CheckEmailIsFreeAsync(student.Email, 0);

            if (ModelState.IsValid)
            {
                student.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                await _studentRepository.AddAsync(student);
                if (await TrySaveAsync(student.Email, 0))
                {
                    this.Toast($"{student.FullName} was added.");
                    await _audit.RecordAsync(User, AuditActions.AccountCreated, $"Student {student.FullName} ({student.Email})");
                    return RedirectToAction(nameof(Index));
                }
            }

            await LoadProgrammesAsync(student.Programme);
            return View(student);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var student = await _studentRepository.GetByIdAsync(id);
            if (student == null) return NotFound();
            await LoadProgrammesAsync(student.Programme);
            return View(student);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("StudentId,FullName,Email,Programme")] Student input)
        {
            if (id != input.StudentId) return NotFound();
            await CheckEmailIsFreeAsync(input.Email, id);

            if (ModelState.IsValid)
            {
                var student = await _studentRepository.GetByIdAsync(id);
                if (student == null) return NotFound();

                student.FullName = input.FullName;
                student.Email = input.Email;
                student.Programme = input.Programme;

                _studentRepository.Update(student);
                if (await TrySaveAsync(input.Email, id))
                {
                    this.Toast($"Changes to {student.FullName} were saved.");
                    return RedirectToAction(nameof(Index));
                }
            }

            await LoadProgrammesAsync(input.Programme);
            return View(input);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var student = await _studentRepository.GetByIdAsync(id);
            if (student == null) return NotFound();

            var storedFiles = await _files.StoredNamesForStudentAsync(id);
            _studentRepository.Delete(student);
            await _studentRepository.SaveChangesAsync();
            await _files.RemoveStoredAsync(storedFiles);
            this.Toast($"{student.FullName} was deleted.");
            await _audit.RecordAsync(User, AuditActions.AccountDeleted, $"Student {student.FullName} ({student.Email})");
            return RedirectToAction(nameof(Index));
        }

        // Programmes are the college's courses; an older free-text value stays selectable so editing doesn't lose it
        private async Task LoadProgrammesAsync(string? current)
        {
            var names = (await _courseRepository.GetAllAsync()).Select(c => c.Name).OrderBy(n => n).ToList();
            if (!string.IsNullOrWhiteSpace(current) && !names.Contains(current))
                names.Insert(0, current);
            ViewBag.Programmes = names;
        }

        // Login looks up lecturers and students by email, so an email must be unique across both
        // Also checks the address is a college student address (Students:EmailDomain)
        private async Task CheckEmailIsFreeAsync(string email, int studentId)
        {
            if (string.IsNullOrWhiteSpace(email)) return;
            if (!_studentOptions.IsStudentEmail(email))
                ModelState.AddModelError(nameof(Student.Email), _studentOptions.EmailDomainMessage);
            else if (await _studentRepository.EmailExistsAsync(email, studentId) || await _lecturerRepository.EmailExistsAsync(email))
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
