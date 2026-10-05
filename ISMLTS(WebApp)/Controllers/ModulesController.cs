using System.Globalization;
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
    public class ModulesController : Controller
    {
        private const string CodeTakenMessage = "Another module already uses that code.";

        private readonly IModuleRepository _moduleRepository;
        private readonly ILecturerRepository _lecturerRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly ISubmissionFileService _files;

        private readonly IAuditLog _audit;

        public ModulesController(
            IModuleRepository moduleRepository,
            ILecturerRepository lecturerRepository,
            IStudentRepository studentRepository,
            ICourseRepository courseRepository,
            ISubmissionFileService files, IAuditLog audit)
        {
            _audit = audit;
            _moduleRepository = moduleRepository;
            _lecturerRepository = lecturerRepository;
            _studentRepository = studentRepository;
            _courseRepository = courseRepository;
            _files = files;
        }

        public async Task<IActionResult> Index(string? q, int page = 1, string? sort = null, string? course = null, string? term = null, int? lecturer = null)
        {
            var filter = new ModuleListFilter(CourseFilter(course), term is CourseTerms.Term1 or CourseTerms.Term2 ? term : null, lecturer);
            var courses = (await _courseRepository.GetAllAsync()).OrderBy(c => c.Code);
            var lecturers = (await _lecturerRepository.GetAllAsync()).OrderBy(l => l.FullName);
            ViewData["ListFilters"] = new List<ListFilter>
            {
                new()
                {
                    Name = "course",
                    Label = "Course",
                    AllText = "Every course",
                    Selected = course,
                    Options = courses.Select(c => (c.CourseId.ToString(CultureInfo.InvariantCulture), c.Code))
                        .Append((ListFilters.None, "Not in a course")).ToList()
                },
                new()
                {
                    Name = "term",
                    Label = "Term",
                    AllText = "Both terms",
                    Selected = filter.Term,
                    Options = new() { (CourseTerms.Term1, "Term 1"), (CourseTerms.Term2, "Term 2") }
                },
                new()
                {
                    Name = "lecturer",
                    Label = "Lecturer",
                    AllText = "Every lecturer",
                    Selected = lecturer?.ToString(CultureInfo.InvariantCulture),
                    Options = lecturers.Select(l => (l.LecturerId.ToString(CultureInfo.InvariantCulture), l.FullName)).ToList()
                }
            };
            return View(await _moduleRepository.SearchAsync(q, page, sort, filter));
        }

        // "none" means modules outside any course (CourseId 0 in the filter); anything that isn't a number is ignored
        private static int? CourseFilter(string? course)
        {
            if (course == ListFilters.None) return 0;
            return int.TryParse(course, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
        }

        public async Task<IActionResult> Details(int id)
        {
            var module = await _moduleRepository.GetByIdWithDetailsAsync(id);
            if (module == null) return NotFound();
            return View(module);
        }

        public async Task<IActionResult> Create()
        {
            await LoadFormData();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Code,Name,LecturerId,Term,CourseId")] Module module)
        {
            await CheckCodeIsFreeAsync(module.Code, 0);
            if (ModelState.IsValid)
            {
                await _moduleRepository.AddAsync(module);
                if (await TrySaveAsync(module.Code, 0))
                {
                    this.Toast($"{module.Code} was added. Enrol students next.");
                    return RedirectToAction(nameof(Enrol), new { id = module.ModuleId });
                }
            }

            await LoadFormData();
            return View(module);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var module = await _moduleRepository.GetByIdAsync(id);
            if (module == null) return NotFound();
            await LoadFormData();
            return View(module);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ModuleId,Code,Name,LecturerId,Term,CourseId")] Module input)
        {
            if (id != input.ModuleId) return NotFound();
            await CheckCodeIsFreeAsync(input.Code, id);
            if (ModelState.IsValid)
            {
                var module = await _moduleRepository.GetByIdAsync(id);
                if (module == null) return NotFound();

                module.Code = input.Code;
                module.Name = input.Name;
                module.LecturerId = input.LecturerId;
                module.Term = input.Term;
                module.CourseId = input.CourseId;

                _moduleRepository.Update(module);
                if (await TrySaveAsync(input.Code, id))
                {
                    this.Toast($"Changes to {module.Code} were saved.");
                    return RedirectToAction(nameof(Index));
                }
            }

            await LoadFormData();
            return View(input);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var module = await _moduleRepository.GetByIdAsync(id);
            if (module == null) return NotFound();

            var storedFiles = await _files.StoredNamesForModuleAsync(id);
            _moduleRepository.Delete(module);
            await _moduleRepository.SaveChangesAsync();
            await _files.RemoveStoredAsync(storedFiles);
            this.Toast($"{module.Code} was deleted.");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Enrol(int id)
        {
            var module = await _moduleRepository.GetByIdWithDetailsAsync(id);
            if (module == null) return NotFound();

            var allStudents = await _studentRepository.GetAllAsync();
            var enrolledIds = module.Students.Select(s => s.StudentId).ToHashSet();

            return View(new EnrolmentViewModel
            {
                ModuleId = module.ModuleId,
                ModuleName = $"{module.Code} - {module.Name}",
                Students = allStudents.OrderBy(s => s.FullName).Select(s => new EnrolmentRow
                {
                    StudentId = s.StudentId,
                    FullName = s.FullName,
                    Programme = s.Programme,
                    IsEnrolled = enrolledIds.Contains(s.StudentId)
                }).ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enrol(int id, List<int> selectedStudentIds)
        {
            var module = await _moduleRepository.GetByIdWithDetailsAsync(id);
            if (module == null) return NotFound();

            selectedStudentIds ??= new List<int>();
            var before = module.Students.Select(s => s.StudentId).ToHashSet();
            module.Students.Clear();
            foreach (var sid in selectedStudentIds)
            {
                var student = await _studentRepository.GetByIdAsync(sid);
                if (student != null) module.Students.Add(student);
            }

            _moduleRepository.Update(module);
            await _moduleRepository.SaveChangesAsync();
            var after = module.Students.Select(s => s.StudentId).ToHashSet();
            await _audit.RecordAsync(User, AuditActions.EnrolmentChanged, $"Module {module.Code}",
                $"{after.Except(before).Count()} added, {before.Except(after).Count()} removed, {after.Count} enrolled now");
            this.Toast($"{module.Students.Count} student(s) are now enrolled in {module.Code}.");
            return RedirectToAction(nameof(Index));
        }

        private async Task CheckCodeIsFreeAsync(string code, int moduleId)
        {
            if (!string.IsNullOrWhiteSpace(code) && await _moduleRepository.CodeExistsAsync(code, moduleId))
                ModelState.AddModelError(nameof(Module.Code), CodeTakenMessage);
        }

        // Two saves racing past the check above are stopped by the unique index
        private async Task<bool> TrySaveAsync(string code, int moduleId)
        {
            try
            {
                await _moduleRepository.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                if (!await _moduleRepository.CodeExistsAsync(code, moduleId)) throw;
                ModelState.AddModelError(nameof(Module.Code), CodeTakenMessage);
                return false;
            }
        }

        private async Task LoadFormData()
        {
            ViewBag.Lecturers = (await _lecturerRepository.GetAllAsync())
                .OrderBy(l => l.FullName)
                .Select(l => new { l.LecturerId, l.FullName }).ToList();
            ViewBag.Courses = (await _courseRepository.GetAllAsync())
                .OrderBy(c => c.Code)
                .Select(c => new { c.CourseId, Display = $"{c.Code} - {c.Name}" }).ToList();
        }
    }
}
