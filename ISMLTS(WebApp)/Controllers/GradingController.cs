using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    // Bulk marking for lecturers: the gradebook, Quick Eval and CSV import. Every action re-checks module ownership.
    [Authorize(Roles = "Lecturer")]
    public class GradingController : Controller
    {
        private readonly IAssessmentRepository _assessmentRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly IMarkRepository _markRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IMarkService _markService;

        public GradingController(
            IAssessmentRepository assessmentRepository,
            IModuleRepository moduleRepository,
            IMarkRepository markRepository,
            ISubmissionRepository submissionRepository,
            IMarkService markService)
        {
            _assessmentRepository = assessmentRepository;
            _moduleRepository = moduleRepository;
            _markRepository = markRepository;
            _submissionRepository = submissionRepository;
            _markService = markService;
        }

        // ---------- Gradebook: every enrolled student for one assessment ----------

        [HttpGet]
        public async Task<IActionResult> Gradebook(int id)
        {
            var (assessment, module) = await GetOwnedAsync(id);
            if (assessment == null || module == null) return NotFound();

            return View(await BuildGradebookAsync(assessment, module, posted: null));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Gradebook(int id, [FromForm(Name = "Rows")] List<GradebookEntry> rows)
        {
            var (assessment, module) = await GetOwnedAsync(id);
            if (assessment == null || module == null) return NotFound();

            rows ??= new List<GradebookEntry>();
            var enrolled = module.Students.Select(s => s.StudentId).ToHashSet();
            foreach (var (index, error) in Grading.ValidateGradebook(rows, enrolled, assessment.MaxScore))
            {
                ModelState.AddModelError($"Rows[{index}].Score", error);
            }

            // All or nothing: one bad row means nothing is saved, so the lecturer fixes it and saves again
            if (!ModelState.IsValid)
            {
                return View(await BuildGradebookAsync(assessment, module, rows));
            }

            var saved = 0;
            foreach (var row in rows.Where(r => r.Score != null))
            {
                var before = await _markRepository.GetByAssessmentAndStudentAsync(assessment.AssessmentId, row.StudentId);
                var changed = before == null || before.Score != row.Score
                    || (before.Feedback ?? string.Empty) != (row.Feedback?.Trim() ?? string.Empty);
                if (!changed) continue;

                await _markService.SaveAsync(module,
                    new MarkEntry(row.StudentId, assessment, assessment.Name, row.Score!.Value, assessment.MaxScore, row.Feedback, DateTime.Today),
                    Grader(), MarkSources.Gradebook, before);
                saved++;
            }

            this.Toast(saved == 0 ? "Nothing changed." : $"Saved {saved} mark(s) for {assessment.Name}.", saved == 0 ? ToastTypes.Info : ToastTypes.Success);
            return RedirectToAction(nameof(Gradebook), new { id });
        }

        // ---------- Helpers ----------

        private async Task<GradebookViewModel> BuildGradebookAsync(Assessment assessment, Module module, IReadOnlyList<GradebookEntry>? posted)
        {
            var marks = (await _markRepository.GetByAssessmentAsync(assessment.AssessmentId)).ToDictionary(m => m.StudentId);
            var submissions = (await _submissionRepository.GetByAssessmentAsync(assessment.AssessmentId)).ToDictionary(s => s.StudentId);
            var postedByStudent = posted?.GroupBy(p => p.StudentId).ToDictionary(g => g.Key, g => g.First());

            return new GradebookViewModel
            {
                AssessmentId = assessment.AssessmentId,
                ModuleId = module.ModuleId,
                AssessmentName = assessment.Name,
                ModuleCode = module.Code,
                OutOf = assessment.MaxScore,
                Released = assessment.MarksReleased,
                // Same order every time, so the row indexes in posted errors line up with what's shown
                Rows = module.Students.OrderBy(s => s.FullName).ThenBy(s => s.StudentId).Select(s =>
                {
                    marks.TryGetValue(s.StudentId, out var mark);
                    submissions.TryGetValue(s.StudentId, out var submission);
                    var postedRow = postedByStudent?.GetValueOrDefault(s.StudentId);
                    return new GradebookRow
                    {
                        StudentId = s.StudentId,
                        FullName = s.FullName,
                        Score = postedRow != null ? postedRow.Score : mark?.Score,
                        Feedback = postedRow != null ? postedRow.Feedback : mark?.Feedback,
                        HasMark = mark != null,
                        SubmissionStatus = submission?.Status ?? "Not Submitted",
                        Link = LinkValidator.IsWebLink(submission?.Link) ? submission?.Link : null
                    };
                }).ToList()
            };
        }

        // The assessment and its module (with students), only if this lecturer teaches it
        private async Task<(Assessment? Assessment, Module? Module)> GetOwnedAsync(int assessmentId)
        {
            var assessment = await _assessmentRepository.GetByIdWithModuleAsync(assessmentId);
            if (assessment?.Module == null || assessment.Module.LecturerId != User.GetUserId()) return (null, null);

            var module = await _moduleRepository.GetByIdWithDetailsAsync(assessment.ModuleId);
            return (assessment, module);
        }

        private Grader Grader() => new(User.GetUserId() ?? 0, User.Identity?.Name ?? "Lecturer");
    }
}
