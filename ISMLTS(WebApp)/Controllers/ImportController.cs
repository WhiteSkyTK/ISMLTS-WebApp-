using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    // Admins add many students or lecturers at once from a CSV file: upload, check the preview, confirm
    [Authorize(Roles = Roles.Admin)]
    public class ImportController : Controller
    {
        public const string Students = "students";
        public const string Lecturers = "lecturers";

        private readonly IUserImportService _importService;
        private readonly ICourseRepository _courseRepository;
        private readonly StudentOptions _studentOptions;

        private readonly IAuditLog _audit;

        public ImportController(IUserImportService importService, ICourseRepository courseRepository, IOptions<StudentOptions> studentOptions, IAuditLog audit)
        {
            _audit = audit;
            _importService = importService;
            _courseRepository = courseRepository;
            _studentOptions = studentOptions.Value;
        }

        [HttpGet]
        public IActionResult Index(string kind = Students) => View(new UserImportViewModel { Kind = Normalise(kind) });

        public IActionResult Template(string kind = Students)
        {
            kind = Normalise(kind);
            var csv = UserImport.Template(kind == Students, _studentOptions);
            return File(Csv.ToUtf8WithBom(csv), "text/csv", $"{kind}-import-template.csv");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(string kind, IFormFile? file)
        {
            var model = new UserImportViewModel { Kind = Normalise(kind) };
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("file", "Choose a CSV file to upload.");
                return View(model);
            }
            if (file.Length > UserImport.MaxFileBytes)
            {
                ModelState.AddModelError("file", "The file is bigger than 1 MB.");
                return View(model);
            }

            using var reader = new StreamReader(file.OpenReadStream(), detectEncodingFromByteOrderMarks: true);
            model.Csv = await reader.ReadToEndAsync();
            model.Result = await _importService.CheckAsync(model.Csv, model.Kind == Students);
            await LoadCoursesAsync();
            return View("Preview", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(string kind, string? csv, int? courseId, string term = CourseTerms.All)
        {
            kind = Normalise(kind);
            var students = kind == Students;
            if (!CourseTerms.IsValid(term)) return BadRequest();

            var course = students && courseId is int id ? await _courseRepository.GetByIdAsync(id) : null;
            if (students && courseId != null && course == null) return NotFound();

            var result = await _importService.CheckAsync(csv ?? string.Empty, students);
            if (result.FileError != null || result.ValidCount == 0)
            {
                this.Toast(result.FileError ?? "There was nobody new to add.", ToastTypes.Danger);
                return RedirectToAction(nameof(Index), new { kind });
            }

            var accounts = await _importService.ImportAsync(result, students, course, term);
            await _audit.RecordAsync(User, AuditActions.AccountsImported, students ? "Students (CSV import)" : "Lecturers (CSV import)",
                $"{accounts.Count} added{(course != null ? $", enrolled in {course.Code}" : null)}");
            var skipped = result.Rows.Count - result.ValidCount;
            return View("Imported", new UserImportDoneViewModel
            {
                Kind = kind,
                Accounts = accounts,
                Skipped = skipped,
                CourseCode = course?.Code
            });
        }

        private static string Normalise(string? kind) => kind == Lecturers ? Lecturers : Students;

        private async Task LoadCoursesAsync() =>
            ViewBag.Courses = (await _courseRepository.GetAllAsync()).OrderBy(c => c.Code).ToList();
    }
}
