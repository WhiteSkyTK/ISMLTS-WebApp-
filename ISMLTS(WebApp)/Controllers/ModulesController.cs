using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Controllers
{
    public class ModulesController : Controller
    {
        private readonly IModuleRepository _moduleRepository;
        private readonly ILecturerRepository _lecturerRepository;
        private readonly IStudentRepository _studentRepository;

        public ModulesController(
            IModuleRepository moduleRepository,
            ILecturerRepository lecturerRepository,
            IStudentRepository studentRepository)
        {
            _moduleRepository = moduleRepository;
            _lecturerRepository = lecturerRepository;
            _studentRepository = studentRepository;
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
            await LoadLecturers();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Code,Name,LecturerId")] Module module)
        {
            if (!ModelState.IsValid)
            {
                await LoadLecturers();
                return View(module);
            }
            await _moduleRepository.AddAsync(module);
            await _moduleRepository.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var module = await _moduleRepository.GetByIdAsync(id);
            if (module == null) return NotFound();
            await LoadLecturers();
            return View(module);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ModuleId,Code,Name,LecturerId")] Module input)
        {
            if (id != input.ModuleId) return NotFound();
            if (!ModelState.IsValid)
            {
                await LoadLecturers();
                return View(input);
            }

            var module = await _moduleRepository.GetByIdAsync(id);
            if (module == null) return NotFound();

            module.Code = input.Code;
            module.Name = input.Name;
            module.LecturerId = input.LecturerId;

            _moduleRepository.Update(module);
            await _moduleRepository.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
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

        private async Task LoadLecturers()
        {
            ViewBag.Lecturers = (await _lecturerRepository.GetAllAsync())
                .Select(l => new { l.LecturerId, l.FullName }).ToList();
        }
    }
}