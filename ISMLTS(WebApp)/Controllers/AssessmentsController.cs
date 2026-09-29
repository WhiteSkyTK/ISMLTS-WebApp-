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
        private readonly IStudentRepository _studentRepository;
        private readonly INotificationService _notifications;

        public AssessmentsController(
            IAssessmentRepository assessmentRepository,
            ISubmissionRepository submissionRepository,
            IModuleRepository moduleRepository,
            IStudentRepository studentRepository,
            INotificationService notifications)
        {
            _assessmentRepository = assessmentRepository;
            _submissionRepository = submissionRepository;
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

            ViewBag.ModuleDisplay = $"{module.Code} - {module.Name}";
            ViewBag.ModuleId = moduleId;
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
        public async Task<IActionResult> Create([Bind("ModuleId,Name,Type,DueDate,Description")] Assessment assessment)
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
        public async Task<IActionResult> Edit(int id, [Bind("AssessmentId,ModuleId,Name,Type,DueDate,Description")] Assessment input)
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
            _assessmentRepository.Delete(assessment);
            await _assessmentRepository.SaveChangesAsync();
            this.Toast($"{assessment.Name} and its submissions were deleted.");
            return RedirectToAction(nameof(ForModule), new { moduleId });
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

            var student = await _studentRepository.GetByIdWithModulesAsync(studentId);
            if (student == null) return NotFound();

            var mySubmissions = (await _submissionRepository.GetByStudentAsync(studentId)).ToDictionary(s => s.AssessmentId);

            var rows = new List<MyAssessmentRow>();
            foreach (var module in student.Modules)
            {
                foreach (var a in await _assessmentRepository.GetByModuleAsync(module.ModuleId))
                {
                    mySubmissions.TryGetValue(a.AssessmentId, out var sub);
                    rows.Add(new MyAssessmentRow
                    {
                        AssessmentId = a.AssessmentId,
                        Name = a.Name,
                        ModuleCode = module.Code,
                        Type = a.Type,
                        DueDate = a.DueDate,
                        Status = sub?.Status ?? "Not Submitted",
                        Link = sub?.Link
                    });
                }
            }

            return View(rows.OrderBy(r => r.DueDate).ToList());
        }

        [Authorize(Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> Submit(int id)
        {
            if (User.GetUserId() is not int studentId) return Forbid();

            var assessment = await GetEnrolledAssessmentAsync(id, studentId);
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

            var assessment = await GetEnrolledAssessmentAsync(id, studentId);
            if (assessment == null) return NotFound();

            link = link?.Trim();
            if (!LinkValidator.IsWebLink(link))
            {
                ModelState.AddModelError(nameof(Submission.Link), "Paste the full link to your work, starting with https://");
                ViewBag.AssessmentDisplay = $"{assessment.Name} ({assessment.Module?.Code})";
                ViewBag.DueDate = assessment.DueDate;
                return View(new Submission { AssessmentId = id, StudentId = studentId, Link = link });
            }

            var existing = await _submissionRepository.GetByAssessmentAndStudentAsync(id, studentId);
            if (existing == null)
            {
                existing = new Submission { AssessmentId = id, StudentId = studentId, Link = link, SubmittedAt = DateTime.UtcNow };
                await _submissionRepository.AddAsync(existing);
            }
            else
            {
                existing.Link = link;
                existing.SubmittedAt = DateTime.UtcNow;
                _submissionRepository.Update(existing);
            }

            await _submissionRepository.SaveChangesAsync();
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

        private async Task<Assessment?> GetEnrolledAssessmentAsync(int id, int studentId)
        {
            var assessment = await _assessmentRepository.GetByIdWithModuleAsync(id);
            return assessment != null && await _studentRepository.IsEnrolledAsync(studentId, assessment.ModuleId) ? assessment : null;
        }
    }
}