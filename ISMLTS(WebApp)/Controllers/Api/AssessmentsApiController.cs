using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models.Api;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers.Api
{
    // The student's assessments (soonest first), handing in a link or a file, and downloading their own files
    public class AssessmentsApiController : StudentApiController
    {
        private readonly IStudentPortalService _portal;
        private readonly ISubmissionFileService _files;

        public AssessmentsApiController(IStudentPortalService portal, ISubmissionFileService files)
        {
            _portal = portal;
            _files = files;
        }

        [HttpGet("assessments")]
        [ProducesResponseType<List<AssessmentDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> List(int? moduleId) =>
            Ok((await _portal.AssessmentsAsync(StudentId))
                .Where(a => moduleId == null || a.ModuleId == moduleId)
                .Select(ApiMap.Assessment)
                .ToList());

        [HttpGet("assessments/{id:int}")]
        [ProducesResponseType<AssessmentDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(int id)
        {
            var assessment = await FindAsync(id);
            return assessment == null ? NotFoundError() : Ok(assessment);
        }

        // Hands in, or replaces, the link to the student's work (an http or https address)
        [HttpPut("assessments/{id:int}/submission")]
        [ProducesResponseType<AssessmentDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Submit(int id, SubmissionRequest request)
        {
            var result = await _portal.SubmitAsync(StudentId, id, request.Link);
            // Only a link was sent here, so "nothing handed in" means the link was empty
            return await ResultAsync(id, result.Outcome == SubmitOutcome.Missing ? result with { Outcome = SubmitOutcome.BadLink } : result);
        }

        // Uploads a file (multipart/form-data, field "file"): PDF, DOCX or ZIP. Earlier files are kept; the newest counts.
        // The link stays as it was.
        [HttpPost("assessments/{id:int}/files")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType<AssessmentDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Upload(int id, IFormFile? file) =>
            await ResultAsync(id, await _portal.SubmitAsync(StudentId, id, null, file, keepLink: true));

        // One of the student's own files: a redirect to a short-lived download link, or the file itself
        [HttpGet("files/{fileId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status302Found)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Download(int fileId)
        {
            var file = await _files.FindForStudentAsync(fileId, StudentId);
            var download = file == null ? null : await _files.OpenAsync(file);
            return file == null || download == null
                ? Error(StatusCodes.Status404NotFound, "not_found", "That file isn't one of yours.")
                : this.SubmissionFile(file, download);
        }

        private async Task<IActionResult> ResultAsync(int id, SubmitResult result) => result.Outcome switch
        {
            SubmitOutcome.NotFound => NotFoundError(),
            SubmitOutcome.Closed => Error(StatusCodes.Status409Conflict, "submissions_closed", result.Message ?? "Submissions for this assessment are closed."),
            SubmitOutcome.BadLink => Error(StatusCodes.Status400BadRequest, "invalid_link", StudentPortalService.BadLinkMessage),
            SubmitOutcome.BadFile => Error(StatusCodes.Status400BadRequest, "invalid_file", result.Message ?? SubmissionFileRules.WrongType),
            SubmitOutcome.Missing => Error(StatusCodes.Status400BadRequest, "missing_file", result.Message ?? StudentPortalService.MissingFileMessage),
            _ => Ok(await FindAsync(id))
        };

        private async Task<AssessmentDto?> FindAsync(int id) =>
            (await _portal.AssessmentsAsync(StudentId)).Where(a => a.AssessmentId == id).Select(ApiMap.Assessment).FirstOrDefault();

        // Another module's assessment looks exactly like one that doesn't exist
        private static ObjectResult NotFoundError() =>
            Error(StatusCodes.Status404NotFound, "not_found", "That assessment isn't in any of your modules.");
    }
}
