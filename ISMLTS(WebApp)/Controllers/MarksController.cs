using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    [Authorize]
    public class MarksController : Controller
    {
        private readonly IMarkRepository _markRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly INotificationService _notifications;

        public MarksController(
            IMarkRepository markRepository,
            IModuleRepository moduleRepository,
            IStudentRepository studentRepository,
            INotificationService notifications)
        {
            _markRepository = markRepository;
            _moduleRepository = moduleRepository;
            _studentRepository = studentRepository;
            _notifications = notifications;
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Index() =>
            View(await _moduleRepository.GetByLecturerAsync(User.GetUserId() ?? 0));

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> ForModule(int moduleId)
        {
            var module = await GetOwnedModuleAsync(moduleId);
            if (module == null) return NotFound();

            var marks = await _markRepository.GetByModuleAsync(moduleId);
            var marksByStudent = marks.GroupBy(m => m.StudentId).ToDictionary(g => g.Key, g => g.ToList());

            var rows = module.Students.Select(s =>
            {
                var studentMarks = marksByStudent.TryGetValue(s.StudentId, out var list) ? list : new List<Mark>();
                return new StudentMarksRow
                {
                    StudentId = s.StudentId,
                    FullName = s.FullName,
                    Marks = studentMarks,
                    AveragePercentage = RiskCalculator.AveragePercentage(studentMarks),
                    IsAtRisk = RiskCalculator.IsAtRisk(studentMarks)
                };
            }).ToList();

            return View(new ModuleMarksViewModel
            {
                ModuleId = module.ModuleId,
                ModuleDisplay = $"{module.Code} - {module.Name}",
                Rows = rows
            });
        }

        [Authorize(Roles = "Lecturer")]
        [HttpGet]
        public async Task<IActionResult> Create(int moduleId, int studentId)
        {
            var module = await GetOwnedModuleAsync(moduleId);
            var student = await _studentRepository.GetByIdAsync(studentId);
            if (module == null || student == null || !await _studentRepository.IsEnrolledAsync(studentId, moduleId)) return NotFound();

            ViewBag.ModuleDisplay = $"{module.Code} - {module.Name}";
            ViewBag.StudentName = student.FullName;
            return View(new Mark { ModuleId = moduleId, StudentId = studentId });
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StudentId,ModuleId,AssessmentName,Score,MaxScore,DateCaptured")] Mark mark)
        {
            var module = await GetOwnedModuleAsync(mark.ModuleId);
            if (module == null || !await _studentRepository.IsEnrolledAsync(mark.StudentId, mark.ModuleId)) return NotFound();
            var student = await _studentRepository.GetByIdAsync(mark.StudentId);

            if (!ModelState.IsValid)
            {
                ViewBag.ModuleDisplay = $"{module.Code} - {module.Name}";
                ViewBag.StudentName = student?.FullName ?? "";
                return View(mark);
            }

            await _markRepository.AddAsync(mark);
            await _markRepository.SaveChangesAsync();
            await _notifications.MarkSavedAsync(mark, module, updated: false);
            this.Toast($"{mark.AssessmentName} mark saved for {student?.FullName}.");
            return RedirectToAction(nameof(ForModule), new { moduleId = mark.ModuleId });
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Edit(int id)
        {
            var mark = await GetOwnedMarkAsync(id);
            if (mark == null) return NotFound();
            return View(mark);
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MarkId,StudentId,ModuleId,AssessmentName,Score,MaxScore,DateCaptured")] Mark input)
        {
            if (id != input.MarkId) return NotFound();

            var mark = await GetOwnedMarkAsync(id);
            if (mark == null) return NotFound();

            if (!ModelState.IsValid)
            {
                input.Student = mark.Student;
                input.Module = mark.Module;
                return View(input);
            }

            mark.AssessmentName = input.AssessmentName;
            mark.Score = input.Score;
            mark.MaxScore = input.MaxScore;
            mark.DateCaptured = input.DateCaptured;

            _markRepository.Update(mark);
            await _markRepository.SaveChangesAsync();
            await _notifications.MarkSavedAsync(mark, mark.Module!, updated: true);
            this.Toast($"{mark.AssessmentName} mark updated for {mark.Student?.FullName}.");
            return RedirectToAction(nameof(ForModule), new { moduleId = mark.ModuleId });
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var mark = await GetOwnedMarkAsync(id);
            if (mark == null) return NotFound();

            _markRepository.Delete(mark);
            await _markRepository.SaveChangesAsync();
            this.Toast($"{mark.AssessmentName} mark deleted for {mark.Student?.FullName}.");
            return RedirectToAction(nameof(ForModule), new { moduleId = mark.ModuleId });
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyMarks()
        {
            if (User.GetUserId() is not int studentId) return Forbid();

            var marks = await _markRepository.GetByStudentAsync(studentId);
            var byModule = marks.GroupBy(m => m.ModuleId).Select(g => new MyModuleMarks
            {
                ModuleDisplay = g.First().Module != null ? $"{g.First().Module!.Code} - {g.First().Module!.Name}" : "Module",
                Marks = g.ToList(),
                AveragePercentage = RiskCalculator.AveragePercentage(g),
                IsAtRisk = RiskCalculator.IsAtRisk(g)
            }).ToList();

            return View(byModule);
        }

        // Another lecturer's module (or mark) looks exactly like one that doesn't exist
        private async Task<Module?> GetOwnedModuleAsync(int moduleId)
        {
            var module = await _moduleRepository.GetByIdWithDetailsAsync(moduleId);
            return module != null && module.LecturerId == User.GetUserId() ? module : null;
        }

        private async Task<Mark?> GetOwnedMarkAsync(int id)
        {
            var mark = await _markRepository.GetByIdWithDetailsAsync(id);
            return mark?.Module != null && mark.Module.LecturerId == User.GetUserId() ? mark : null;
        }
    }
}