using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models.Api;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers.Api
{
    // The signed-in student, their modules and progress, released marks, attendance and announcements
    public class MeApiController : StudentApiController
    {
        private const int AnnouncementCount = 30;

        private readonly IStudentPortalService _portal;
        private readonly IMarkRepository _marks;
        private readonly IAnnouncementRepository _announcements;

        public MeApiController(IStudentPortalService portal, IMarkRepository marks, IAnnouncementRepository announcements)
        {
            _portal = portal;
            _marks = marks;
            _announcements = announcements;
        }

        [HttpGet("me")]
        [ProducesResponseType<StudentDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Me()
        {
            var student = await _portal.StudentAsync(StudentId);
            return student == null ? Error(StatusCodes.Status404NotFound, "not_found", "Your account no longer exists.") : Ok(ApiMap.Student(student));
        }

        // Average (released marks only), attendance, submissions and risk for each module
        [HttpGet("modules")]
        [ProducesResponseType<List<ModuleDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Modules()
        {
            var student = await _portal.StudentAsync(StudentId);
            var progress = await _portal.ProgressAsync(StudentId);
            if (student == null || progress == null) return Error(StatusCodes.Status404NotFound, "not_found", "Your account no longer exists.");

            var modules = student.Modules.ToDictionary(m => m.ModuleId);
            return Ok(progress.Modules.Select(p => ApiMap.Module(p, modules.GetValueOrDefault(p.ModuleId))).ToList());
        }

        // Only marks the lecturer has released, newest first
        [HttpGet("marks")]
        [ProducesResponseType<List<MarkListItemDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Marks() =>
            Ok((await _marks.GetByStudentAsync(StudentId))
                .Where(m => m.IsVisibleToStudent)
                .OrderByDescending(m => m.DateCaptured).ThenByDescending(m => m.MarkId)
                .Select(ApiMap.MarkItem)
                .ToList());

        [HttpGet("attendance")]
        [ProducesResponseType<List<AttendanceDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Attendance() =>
            Ok((await _portal.AttendanceAsync(StudentId)).Select(ApiMap.Attendance).ToList());

        // Posts to the student's modules plus college-wide posts for students, newest first
        [HttpGet("announcements")]
        [ProducesResponseType<List<AnnouncementDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Announcements()
        {
            var student = await _portal.StudentAsync(StudentId);
            var moduleIds = student?.Modules.Select(m => m.ModuleId).ToList() ?? new List<int>();
            return Ok((await _announcements.GetForStudentAsync(moduleIds, AnnouncementCount)).Select(ApiMap.Announcement).ToList());
        }
    }
}
