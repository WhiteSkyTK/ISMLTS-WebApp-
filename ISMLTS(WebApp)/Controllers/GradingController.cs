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

        // ---------- Quick Eval: submitted work waiting for a mark, across all my modules ----------

        [HttpGet]
        public async Task<IActionResult> QuickEval(int skip = 0)
        {
            var queue = await QuickEvalQueueAsync();
            var position = Math.Clamp(skip, 0, Math.Max(0, queue.Count - 1));
            return View(new QuickEvalViewModel
            {
                Remaining = queue.Count,
                Position = position,
                Current = queue.Count == 0 ? null : queue[position]
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickEval(int assessmentId, int studentId, decimal? score, string? feedback, int skip = 0)
        {
            var (assessment, module) = await GetOwnedAsync(assessmentId);
            if (assessment == null || module == null || module.Students.All(s => s.StudentId != studentId)) return NotFound();

            var queue = await QuickEvalQueueAsync();
            var current = queue.FirstOrDefault(s => s.AssessmentId == assessmentId && s.StudentId == studentId);
            if (current == null)
            {
                this.Toast("That work already has a mark.", ToastTypes.Info);
                return RedirectToAction(nameof(QuickEval), new { skip });
            }

            var error = ModelState.IsValid
                ? MarkRules.CheckScore(score, assessment.MaxScore) ?? MarkRules.CheckFeedback(feedback)
                : "Enter the score as a number.";
            if (error != null)
            {
                ModelState.AddModelError(nameof(QuickEvalViewModel.Score), error);
                return View(new QuickEvalViewModel
                {
                    Remaining = queue.Count,
                    Position = queue.IndexOf(current),
                    Current = current,
                    Score = score,
                    Feedback = feedback
                });
            }

            await _markService.SaveAsync(module,
                new MarkEntry(studentId, assessment, assessment.Name, score!.Value, assessment.MaxScore, feedback, DateTime.Today),
                Grader(), MarkSources.QuickEval);
            this.Toast($"Saved {MarkRules.Format(score.Value)}/{MarkRules.Format(assessment.MaxScore)} for {current.Student?.FullName}.");

            // The saved item leaves the queue, so the same position now holds the next one
            return RedirectToAction(nameof(QuickEval), new { skip });
        }

        // ---------- Helpers ----------

        private async Task<List<Submission>> QuickEvalQueueAsync()
        {
            var moduleIds = (await _moduleRepository.GetByLecturerAsync(User.GetUserId() ?? 0)).Select(m => m.ModuleId).ToList();
            var assessmentIds = (await _assessmentRepository.GetByModulesAsync(moduleIds)).Select(a => a.AssessmentId).ToList();
            var submissions = await _submissionRepository.GetByAssessmentsAsync(assessmentIds);
            var marked = await _markRepository.GetMarkedPairsAsync(assessmentIds);
            return Grading.QuickEvalQueue(submissions, marked);
        }

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
