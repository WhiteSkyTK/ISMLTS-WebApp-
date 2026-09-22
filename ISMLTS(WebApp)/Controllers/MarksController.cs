using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    public class MarksController : Controller
    {
        private readonly IMarkRepository _markRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly IStudentRepository _studentRepository;

        public MarksController(
            IMarkRepository markRepository,
            IModuleRepository moduleRepository,
            IStudentRepository studentRepository)
        {
            _markRepository = markRepository;
            _moduleRepository = moduleRepository;
            _studentRepository = studentRepository;
        }

        public async Task<IActionResult> Index() => View(await _moduleRepository.GetAllWithLecturerAsync());

        public async Task<IActionResult> ForModule(int moduleId)
        {
            var module = await _moduleRepository.GetByIdWithDetailsAsync(moduleId);
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

        [HttpGet]
        public async Task<IActionResult> Create(int moduleId, int studentId)
        {
            var module = await _moduleRepository.GetByIdAsync(moduleId);
            var student = await _studentRepository.GetByIdAsync(studentId);
            if (module == null || student == null) return NotFound();

            ViewBag.ModuleDisplay = $"{module.Code} - {module.Name}";
            ViewBag.StudentName = student.FullName;
            return View(new Mark { ModuleId = moduleId, StudentId = studentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StudentId,ModuleId,AssessmentName,Score,MaxScore,DateCaptured")] Mark mark)
        {
            if (!ModelState.IsValid)
            {
                var module = await _moduleRepository.GetByIdAsync(mark.ModuleId);
                var student = await _studentRepository.GetByIdAsync(mark.StudentId);
                ViewBag.ModuleDisplay = module != null ? $"{module.Code} - {module.Name}" : "";
                ViewBag.StudentName = student?.FullName ?? "";
                return View(mark);
            }

            await _markRepository.AddAsync(mark);
            await _markRepository.SaveChangesAsync();
            return RedirectToAction(nameof(ForModule), new { moduleId = mark.ModuleId });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var mark = await _markRepository.GetByIdWithDetailsAsync(id);
            if (mark == null) return NotFound();
            return View(mark);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MarkId,StudentId,ModuleId,AssessmentName,Score,MaxScore,DateCaptured")] Mark input)
        {
            if (id != input.MarkId) return NotFound();
            if (!ModelState.IsValid) return View(input);

            var mark = await _markRepository.GetByIdAsync(id);
            if (mark == null) return NotFound();

            mark.AssessmentName = input.AssessmentName;
            mark.Score = input.Score;
            mark.MaxScore = input.MaxScore;
            mark.DateCaptured = input.DateCaptured;

            _markRepository.Update(mark);
            await _markRepository.SaveChangesAsync();
            return RedirectToAction(nameof(ForModule), new { moduleId = mark.ModuleId });
        }

        public async Task<IActionResult> Delete(int id)
        {
            var mark = await _markRepository.GetByIdWithDetailsAsync(id);
            if (mark == null) return NotFound();
            return View(mark);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var mark = await _markRepository.GetByIdAsync(id);
            if (mark == null) return RedirectToAction(nameof(Index));

            var moduleId = mark.ModuleId;
            _markRepository.Delete(mark);
            await _markRepository.SaveChangesAsync();
            return RedirectToAction(nameof(ForModule), new { moduleId });
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyMarks()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (idClaim == null || !int.TryParse(idClaim, out var studentId)) return Forbid();

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
    }
}