using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    // Uploaded work opens only for the student who handed it in and the lecturer who teaches its module;
    // for anyone else the file looks like it doesn't exist
    [Authorize]
    public class SubmissionFilesController : Controller
    {
        private readonly ISubmissionFileService _files;

        public SubmissionFilesController(ISubmissionFileService files)
        {
            _files = files;
        }

        [Authorize(Roles = Roles.Student + "," + Roles.Lecturer)]
        public async Task<IActionResult> Download(int id)
        {
            if (User.GetUserId() is not int userId) return Forbid();

            var file = User.IsInRole(Roles.Lecturer)
                ? await _files.FindForLecturerAsync(id, userId)
                : await _files.FindForStudentAsync(id, userId);
            if (file == null) return NotFound();

            var download = await _files.OpenAsync(file);
            return download == null ? NotFound() : this.SubmissionFile(file, download);
        }
    }
}
