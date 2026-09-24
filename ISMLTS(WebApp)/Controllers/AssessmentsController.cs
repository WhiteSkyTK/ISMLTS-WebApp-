using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Controllers
{
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

        public async Task<IActionResult> Index() => View(await _moduleRepository.GetAllWithLecturerAsync());

        public async Task<IActionResult> ForModule(int moduleId)
        {
            var module = await _moduleRepository.GetByIdAsync(moduleId);
            if (module == null) return NotFound();

            ViewBag.ModuleDisplay = $"{module.Code} - {module.Name}";
            ViewBag.ModuleId = moduleId;
            return View(await _assessmentRepository.GetByModuleAsync(moduleId));
        }

        [HttpGet]
        public IActionResult Create(int moduleId) =>
            View(new Assessment { ModuleId = moduleId, DueDate = DateTime.Today.AddDays(7) });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ModuleId,Name,Type,DueDate,Description")] Assessment assessment)
        {
            if (!ModelState.IsValid) return View(assessment);
            await _assessmentRepository.AddAsync(assessment);
            await _assessmentRepository.SaveChangesAsync();
            return RedirectToAction(nameof(ForModule), new { moduleId = assessment.ModuleId });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var assessment = await _assessmentRepository.GetByIdAsync(id);
            if (assessment == null) return NotFound();
            return View(assessment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AssessmentId,ModuleId,Name,Type,DueDate,Description")] Assessment input)
        {
            if (id != input.AssessmentId) return NotFound();
            if (!ModelState.IsValid) return View(input);

            var assessment = await _assessmentRepository.GetByIdAsync(id);
            if (assessment == null) return NotFound();

            assessment.Name = input.Name;
            assessment.Type = input.Type;
            assessment.DueDate = input.DueDate;
            assessment.Description = input.Description;

            _assessmentRepository.Update(assessment);
            await _assessmentRepository.SaveChangesAsync();
            return RedirectToAction(nameof(ForModule), new { moduleId = assessment.ModuleId });
        }

        public async Task<IActionResult> Delete(int id)
        {
            var assessment = await _assessmentRepository.GetByIdWithModuleAsync(id);
            if (assessment == null) return NotFound();
            return View(assessment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var assessment = await _assessmentRepository.GetByIdAsync(id);
            if (assessment == null) return RedirectToAction(nameof(Index));

            var moduleId = assessment.ModuleId;
            _assessmentRepository.Delete(assessment);
            await _assessmentRepository.SaveChangesAsync();
            return RedirectToAction(nameof(ForModule), new { moduleId });
        }

        public async Task<IActionResult> Submissions(int id)
        {
            var assessment = await _assessmentRepository.GetByIdWithModuleAsync(id);
            if (assessment == null) return NotFound();

            var module = await _moduleRepository.GetByIdWithDetailsAsync(assessment.ModuleId);
            var submissions = (await _submissionRepository.GetByAssessmentAsync(id)).ToDictionary(s => s.StudentId);

            var rows = module!.Students.Select(s =>
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

            var assessment = await _assessmentRepository.GetByIdWithModuleAsync(id);
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
    }
}