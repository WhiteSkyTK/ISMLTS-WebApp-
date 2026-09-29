using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

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

        public ModulesController(
            IModuleRepository moduleRepository,
            ILecturerRepository lecturerRepository,
            IStudentRepository studentRepository,
            ICourseRepository courseRepository)
        {
            _moduleRepository = moduleRepository;
            _lecturerRepository = lecturerRepository;
            _studentRepository = studentRepository;
            _courseRepository = courseRepository;
        }

        public async Task<IActionResult> Index() => View(await _moduleRepository.GetAllWithLecturerAsync());

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
                if (await TrySaveAsync(module.Code, 0)) return RedirectToAction(nameof(Index));
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
                if (await TrySaveAsync(input.Code, id)) return RedirectToAction(nameof(Index));
            }

            await LoadFormData();
            return View(input);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var module = await _moduleRepository.GetByIdAsync(id);
            if (module == null) return NotFound();
            return View(module);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var module = await _moduleRepository.GetByIdAsync(id);
            if (module != null)
            {
                _moduleRepository.Delete(module);
                await _moduleRepository.SaveChangesAsync();
            }
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
                Students = allStudents.Select(s => new EnrolmentRow
                {
                    StudentId = s.StudentId,
                    FullName = s.FullName,
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
            module.Students.Clear();
            foreach (var sid in selectedStudentIds)
            {
                var student = await _studentRepository.GetByIdAsync(sid);
                if (student != null) module.Students.Add(student);
            }

            _moduleRepository.Update(module);
            await _moduleRepository.SaveChangesAsync();
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
                .Select(l => new { l.LecturerId, l.FullName }).ToList();
            ViewBag.Courses = (await _courseRepository.GetAllAsync())
                .Select(c => new { c.CourseId, Display = $"{c.Code} - {c.Name}" }).ToList();
        }
    }
}