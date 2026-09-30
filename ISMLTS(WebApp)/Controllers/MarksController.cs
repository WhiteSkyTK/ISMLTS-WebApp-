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
        private readonly IAssessmentRepository _assessmentRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IMarkService _markService;

        public MarksController(
            IMarkRepository markRepository,
            IModuleRepository moduleRepository,
            IStudentRepository studentRepository,
            IAssessmentRepository assessmentRepository,
            ISubmissionRepository submissionRepository,
            IMarkService markService)
        {
            _markRepository = markRepository;
            _moduleRepository = moduleRepository;
            _studentRepository = studentRepository;
            _assessmentRepository = assessmentRepository;
            _submissionRepository = submissionRepository;
            _markService = markService;
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Index() =>
            View(await _moduleRepository.GetByLecturerAsync(User.GetUserId() ?? 0));

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> ForModule(int moduleId)
        {
            var module = await GetOwnedModuleAsync(moduleId);
            if (module == null) return NotFound();

            var assessments = (await _assessmentRepository.GetByModuleAsync(moduleId)).ToList();
            var submissions = await _submissionRepository.GetByAssessmentsAsync(assessments.Select(a => a.AssessmentId).ToList());
            var marks = await _markRepository.GetByModuleAsync(moduleId);

            return View(Markbook.Build(module, module.Students, assessments, marks, submissions));
        }

        [Authorize(Roles = "Lecturer")]
        [HttpGet]
        public async Task<IActionResult> Create(int moduleId, int studentId, int? assessmentId)
        {
            var module = await GetOwnedModuleAsync(moduleId);
            var student = await _studentRepository.GetByIdAsync(studentId);
            if (module == null || student == null || !await _studentRepository.IsEnrolledAsync(studentId, moduleId)) return NotFound();

            await LoadFormDataAsync(module, student.FullName);
            return View(new MarkForm { ModuleId = moduleId, StudentId = studentId, AssessmentId = assessmentId });
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StudentId,ModuleId,AssessmentId,OtherName,Score,OutOf,Feedback,DateCaptured")] MarkForm form)
        {
            var module = await GetOwnedModuleAsync(form.ModuleId);
            var student = await _studentRepository.GetByIdAsync(form.StudentId);
            if (module == null || student == null || !await _studentRepository.IsEnrolledAsync(form.StudentId, form.ModuleId)) return NotFound();

            var assessment = await ValidateAsync(form, module, markId: null);
            if (!ModelState.IsValid)
            {
                await LoadFormDataAsync(module, student.FullName);
                return View(form);
            }

            var mark = await _markService.SaveAsync(module, ToEntry(form, assessment), Grader(), MarkSources.Form);
            this.Toast($"{mark.AssessmentName} mark saved for {student.FullName}.");
            return RedirectToAction(nameof(ForModule), new { moduleId = module.ModuleId });
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Edit(int id)
        {
            var mark = await GetOwnedMarkAsync(id);
            if (mark == null) return NotFound();

            await LoadFormDataAsync(mark.Module!, mark.Student?.FullName ?? string.Empty, mark.MarkId);
            return View(new MarkForm
            {
                MarkId = mark.MarkId,
                ModuleId = mark.ModuleId,
                StudentId = mark.StudentId,
                AssessmentId = mark.AssessmentId,
                OtherName = mark.AssessmentId == null ? mark.AssessmentName : null,
                Score = mark.Score,
                OutOf = mark.MaxScore,
                Feedback = mark.Feedback,
                DateCaptured = mark.DateCaptured
            });
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MarkId,AssessmentId,OtherName,Score,OutOf,Feedback,DateCaptured")] MarkForm form)
        {
            if (id != form.MarkId) return NotFound();

            var mark = await GetOwnedMarkAsync(id);
            if (mark == null) return NotFound();
            form.ModuleId = mark.ModuleId;
            form.StudentId = mark.StudentId;

            var assessment = await ValidateAsync(form, mark.Module!, mark.MarkId);
            if (!ModelState.IsValid)
            {
                await LoadFormDataAsync(mark.Module!, mark.Student?.FullName ?? string.Empty, mark.MarkId);
                return View(form);
            }

            await _markService.SaveAsync(mark.Module!, ToEntry(form, assessment), Grader(), MarkSources.Form, existing: mark);
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

            await _markService.DeleteAsync(mark, Grader());
            this.Toast($"{mark.AssessmentName} mark deleted for {mark.Student?.FullName}.");
            return RedirectToAction(nameof(ForModule), new { moduleId = mark.ModuleId });
        }

        // Every mark in the module as a spreadsheet
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Export(int moduleId)
        {
            var module = await GetOwnedModuleAsync(moduleId);
            if (module == null) return NotFound();

            var csv = Csv.MarksExport(await _markRepository.GetByModuleAsync(moduleId));
            return File(Csv.ToUtf8WithBom(csv), "text/csv", $"{module.Code}-marks-{DateTime.Today:yyyy-MM-dd}.csv");
        }

        // Students only see marks that are released (or not tied to an assessment)
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyMarks()
        {
            if (User.GetUserId() is not int studentId) return Forbid();

            var marks = (await _markRepository.GetByStudentAsync(studentId)).Where(m => m.IsVisibleToStudent);
            var byModule = marks.GroupBy(m => m.ModuleId).Select(g => new MyModuleMarks
            {
                ModuleDisplay = g.First().Module != null ? $"{g.First().Module!.Code} - {g.First().Module!.Name}" : "Module",
                Marks = g.OrderBy(m => m.DateCaptured).ToList(),
                AveragePercentage = RiskCalculator.AveragePercentage(g),
                IsAtRisk = RiskCalculator.IsAtRisk(g)
            }).ToList();

            return View(byModule);
        }

        // Checks the form with MarkRules; returns the linked assessment when one was picked
        private async Task<Assessment?> ValidateAsync(MarkForm form, Module module, int? markId)
        {
            Assessment? assessment = null;
            if (form.AssessmentId is int assessmentId)
            {
                assessment = await _assessmentRepository.GetByIdAsync(assessmentId);
                if (assessment == null || assessment.ModuleId != module.ModuleId)
                {
                    ModelState.AddModelError(nameof(MarkForm.AssessmentId), "Pick an assessment from this module.");
                    return null;
                }

                var existing = await _markRepository.GetByAssessmentAndStudentAsync(assessmentId, form.StudentId);
                if (existing != null && existing.MarkId != markId)
                {
                    ModelState.AddModelError(nameof(MarkForm.AssessmentId), "This student already has a mark for that assessment. Edit that mark instead.");
                }
            }
            else if (string.IsNullOrWhiteSpace(form.OtherName))
            {
                ModelState.AddModelError(nameof(MarkForm.OtherName), "Pick an assessment, or give the mark a name.");
            }

            var outOf = assessment?.MaxScore ?? form.OutOf;
            if (assessment == null) AddError(nameof(MarkForm.OutOf), MarkRules.CheckOutOf(outOf));
            if (outOf is decimal total && ModelState.GetValidationState(nameof(MarkForm.Score)) != Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid)
            {
                AddError(nameof(MarkForm.Score), MarkRules.CheckScore(form.Score, total));
            }
            AddError(nameof(MarkForm.Feedback), MarkRules.CheckFeedback(form.Feedback));
            return assessment;
        }

        private void AddError(string key, string? error)
        {
            if (error != null) ModelState.AddModelError(key, error);
        }

        private static MarkEntry ToEntry(MarkForm form, Assessment? assessment) => new(
            form.StudentId,
            assessment,
            assessment?.Name ?? form.OtherName!.Trim(),
            form.Score!.Value,
            assessment?.MaxScore ?? form.OutOf!.Value,
            form.Feedback,
            form.DateCaptured);

        private Grader Grader() => new(User.GetUserId() ?? 0, User.Identity?.Name ?? "Lecturer");

        private async Task LoadFormDataAsync(Module module, string studentName, int? markId = null)
        {
            ViewBag.ModuleDisplay = $"{module.Code} - {module.Name}";
            ViewBag.StudentName = studentName;
            ViewBag.Assessments = (await _assessmentRepository.GetByModuleAsync(module.ModuleId))
                .Select(a => new { a.AssessmentId, Display = $"{a.Name} (out of {MarkRules.Format(a.MaxScore)})" })
                .ToList();
            ViewBag.History = markId is int id ? await _markService.HistoryAsync(id) : new List<MarkChange>();
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
