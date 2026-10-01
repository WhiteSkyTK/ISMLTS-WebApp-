using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    [Authorize]
    public class AssessmentsController : Controller
    {
        private readonly IAssessmentRepository _assessmentRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly INotificationService _notifications;
        private readonly IMarkRepository _markRepository;
        private readonly IMarkService _markService;
        private readonly IStudentPortalService _portal;

        public AssessmentsController(
            IAssessmentRepository assessmentRepository,
            ISubmissionRepository submissionRepository,
            IModuleRepository moduleRepository,
            INotificationService notifications,
            IMarkRepository markRepository,
            IMarkService markService,
            IStudentPortalService portal)
        {
            _portal = portal;
            _assessmentRepository = assessmentRepository;
            _submissionRepository = submissionRepository;
            _moduleRepository = moduleRepository;
            _notifications = notifications;
            _markRepository = markRepository;
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

            ViewBag.ModuleDisplay = $"{module.Code} - {module.Name}";
            ViewBag.ModuleId = moduleId;
            ViewBag.EnrolledCount = module.Students.Count;
            ViewBag.MarkedCounts = (await _markRepository.GetByModuleAsync(moduleId))
                .Where(m => m.AssessmentId != null)
                .GroupBy(m => m.AssessmentId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());
            return View(await _assessmentRepository.GetByModuleAsync(moduleId));
        }

        [Authorize(Roles = "Lecturer")]
        [HttpGet]
        public async Task<IActionResult> Create(int moduleId)
        {
            var module = await GetOwnedModuleAsync(moduleId);
            if (module == null) return NotFound();
            return View(new Assessment { ModuleId = moduleId, Module = module, DueDate = DateTime.Today.AddDays(7) });
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ModuleId,Name,Type,DueDate,Description,MaxScore")] Assessment assessment)
        {
            var module = await GetOwnedModuleAsync(assessment.ModuleId);
            if (module == null) return NotFound();
            if (!ModelState.IsValid)
            {
                assessment.Module = module;
                return View(assessment);
            }

            await _assessmentRepository.AddAsync(assessment);
            await _assessmentRepository.SaveChangesAsync();
            await _notifications.AssessmentPostedAsync(assessment, module);
            this.Toast($"{assessment.Name} was added to {module.Code}. Enrolled students have been notified.");
            return RedirectToAction(nameof(ForModule), new { moduleId = assessment.ModuleId });
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Edit(int id)
        {
            var assessment = await GetOwnedAssessmentAsync(id);
            if (assessment == null) return NotFound();
            return View(assessment);
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AssessmentId,ModuleId,Name,Type,DueDate,Description,MaxScore")] Assessment input)
        {
            if (id != input.AssessmentId) return NotFound();

            var assessment = await GetOwnedAssessmentAsync(id);
            if (assessment == null) return NotFound();

            if (!ModelState.IsValid)
            {
                input.Module = assessment.Module;
                return View(input);
            }

            assessment.Name = input.Name;
            assessment.Type = input.Type;
            assessment.DueDate = input.DueDate;
            assessment.Description = input.Description;
            assessment.MaxScore = input.MaxScore;

            _assessmentRepository.Update(assessment);
            await _assessmentRepository.SaveChangesAsync();
            this.Toast($"Changes to {assessment.Name} were saved.");
            return RedirectToAction(nameof(ForModule), new { moduleId = assessment.ModuleId });
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var assessment = await GetOwnedAssessmentAsync(id);
            if (assessment == null) return NotFound();

            var moduleId = assessment.ModuleId;
            await _markService.DeleteAssessmentAsync(assessment);
            this.Toast($"{assessment.Name} and its submissions were deleted. Its marks were kept.");
            return RedirectToAction(nameof(ForModule), new { moduleId });
        }

        // Students only see this assessment's marks once they're released; releasing notifies everyone with a mark.
        // The markbook and gradebook send returnUrl so the lecturer stays on the page they released from.
        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Release(int id, bool released, string? returnUrl)
        {
            var assessment = await GetOwnedAssessmentAsync(id);
            if (assessment == null) return NotFound();

            await _markService.SetReleasedAsync(assessment, assessment.Module!, released);
            this.Toast(released
                ? $"Marks for {assessment.Name} are released. Students with a mark have been notified."
                : $"Marks for {assessment.Name} are hidden from students again.", released ? ToastTypes.Success : ToastTypes.Info);
            if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
            return RedirectToAction(nameof(ForModule), new { moduleId = assessment.ModuleId });
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Submissions(int id)
        {
            var assessment = await GetOwnedAssessmentAsync(id);
            if (assessment == null) return NotFound();

            var module = await GetOwnedModuleAsync(assessment.ModuleId);
            if (module == null) return NotFound();
            var submissions = (await _submissionRepository.GetByAssessmentAsync(id)).ToDictionary(s => s.StudentId);

            var rows = module.Students.Select(s =>
            {
                submissions.TryGetValue(s.StudentId, out var sub);
                return new SubmissionRow
                {
                    StudentId = s.StudentId,
                    FullName = s.FullName,
                    SubmissionId = sub?.SubmissionId,
                    SubmittedAt = sub?.SubmittedAt,
                    Link = LinkValidator.IsWebLink(sub?.Link) ? sub?.Link : null, // hides links saved before validation existed
                    Status = sub?.Status ?? "Not Submitted"
                };
            }).ToList();

            return View(new AssessmentSubmissionsViewModel
            {
                AssessmentId = assessment.AssessmentId,
                ModuleId = module.ModuleId,
                AssessmentDisplay = $"{assessment.Name} ({module.Code})",
                DueDate = assessment.DueDate,
                Rows = rows
            });
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyAssessments()
        {
            if (User.GetUserId() is not int studentId) return Forbid();
            return View(await _portal.AssessmentsAsync(studentId));
        }

        [Authorize(Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> Submit(int id)
        {
            if (User.GetUserId() is not int studentId) return Forbid();

            var assessment = await _portal.EnrolledAssessmentAsync(studentId, id);
            if (assessment == null) return NotFound();

            var existing = await _submissionRepository.GetByAssessmentAndStudentAsync(id, studentId);

            ViewBag.AssessmentDisplay = $"{assessment.Name} ({assessment.Module?.Code})";
            ViewBag.DueDate = assessment.DueDate;
            return View(new Submission { AssessmentId = id, StudentId = studentId, Link = existing?.Link });
        }

        [Authorize(Roles = "Student")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int id, string? link)
        {
            if (User.GetUserId() is not int studentId) return Forbid();

            var result = await _portal.SubmitAsync(studentId, id, link);
            if (result.Outcome == SubmitOutcome.NotFound || result.Assessment == null) return NotFound();
            var assessment = result.Assessment;

            if (result.Outcome == SubmitOutcome.BadLink)
            {
                ModelState.AddModelError(nameof(Submission.Link), StudentPortalService.BadLinkMessage);
                ViewBag.AssessmentDisplay = $"{assessment.Name} ({assessment.Module?.Code})";
                ViewBag.DueDate = assessment.DueDate;
                return View(new Submission { AssessmentId = id, StudentId = studentId, Link = link?.Trim() });
            }

            this.Toast($"Your work for {assessment.Name} was submitted.");
            return RedirectToAction(nameof(MyAssessments));
        }

        // Another lecturer's module (or assessment) looks exactly like one that doesn't exist
        private async Task<Module?> GetOwnedModuleAsync(int moduleId)
        {
            var module = await _moduleRepository.GetByIdWithDetailsAsync(moduleId);
            return module != null && module.LecturerId == User.GetUserId() ? module : null;
        }

        private async Task<Assessment?> GetOwnedAssessmentAsync(int id)
        {
            var assessment = await _assessmentRepository.GetByIdWithModuleAsync(id);
            return assessment?.Module != null && assessment.Module.LecturerId == User.GetUserId() ? assessment : null;
        }
    }
}