using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Controllers
{
    [Authorize]
    public class AssessmentsController : Controller
    {
        private readonly IAssessmentRepository _assessmentRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly IStudentRepository _studentRepository;

        public AssessmentsController(
            IAssessmentRepository assessmentRepository,
            ISubmissionRepository submissionRepository,
            IModuleRepository moduleRepository,
            IStudentRepository studentRepository)
        {
            _assessmentRepository = assessmentRepository;
            _submissionRepository = submissionRepository;
            _moduleRepository = moduleRepository;
            _studentRepository = studentRepository;
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
            if (await GetOwnedModuleAsync(moduleId) == null) return NotFound();
            return View(new Assessment { ModuleId = moduleId, DueDate = DateTime.Today.AddDays(7) });
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ModuleId,Name,Type,DueDate,Description")] Assessment assessment)
        {
            if (await GetOwnedModuleAsync(assessment.ModuleId) == null) return NotFound();
            if (!ModelState.IsValid) return View(assessment);
            await _assessmentRepository.AddAsync(assessment);
            await _assessmentRepository.SaveChangesAsync();
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

            if (!ModelState.IsValid) return View(input);

            assessment.Name = input.Name;
            assessment.Type = input.Type;
            assessment.DueDate = input.DueDate;
            assessment.Description = input.Description;

            _assessmentRepository.Update(assessment);
            await _assessmentRepository.SaveChangesAsync();
            return RedirectToAction(nameof(ForModule), new { moduleId = assessment.ModuleId });
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Delete(int id)
        {
            var assessment = await GetOwnedAssessmentAsync(id);
            if (assessment == null) return NotFound();
            return View(assessment);
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var assessment = await GetOwnedAssessmentAsync(id);
            if (assessment == null) return RedirectToAction(nameof(Index));

            var moduleId = assessment.ModuleId;
            _assessmentRepository.Delete(assessment);
            await _assessmentRepository.SaveChangesAsync();
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
                    Link = sub?.Link,
                    Status = sub?.Status ?? "Not Submitted"
                };
            }).ToList();

            return View(new AssessmentSubmissionsViewModel
            {
                AssessmentId = assessment.AssessmentId,
                AssessmentDisplay = $"{assessment.Name} ({module.Code})",
                DueDate = assessment.DueDate,
                Rows = rows
            });
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyAssessments()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (idClaim == null || !int.TryParse(idClaim, out var studentId)) return Forbid();

            var student = await _studentRepository.GetByIdWithModulesAsync(studentId);
            if (student == null) return NotFound();

            var mySubmissions = (await _submissionRepository.GetByStudentAsync(studentId)).ToDictionary(s => s.AssessmentId);
            var myModuleIds = student.Modules.Select(m => m.ModuleId).ToHashSet();

            var rows = new List<MyAssessmentRow>();
            foreach (var moduleId in myModuleIds)
            {
                foreach (var a in await _assessmentRepository.GetByModuleAsync(moduleId))
                {
                    mySubmissions.TryGetValue(a.AssessmentId, out var sub);
                    rows.Add(new MyAssessmentRow
                    {
                        AssessmentId = a.AssessmentId,
                        Name = a.Name,
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
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (idClaim == null || !int.TryParse(idClaim, out var studentId)) return Forbid();

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
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (idClaim == null || !int.TryParse(idClaim, out var studentId)) return Forbid();

            if (await GetEnrolledAssessmentAsync(id, studentId) == null) return NotFound();

            var existing = await _submissionRepository.GetByAssessmentAndStudentAsync(id, studentId);
            if (existing == null)
            {
                existing = new Submission { AssessmentId = id, StudentId = studentId, Link = link, SubmittedAt = DateTime.Now };
                await _submissionRepository.AddAsync(existing);
            }
            else
            {
                existing.Link = link;
                existing.SubmittedAt = DateTime.Now;
                _submissionRepository.Update(existing);
            }

            await _submissionRepository.SaveChangesAsync();
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