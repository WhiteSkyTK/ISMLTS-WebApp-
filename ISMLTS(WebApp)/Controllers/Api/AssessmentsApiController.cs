using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models.Api;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers.Api
{
    // The student's assessments (soonest first) and handing in a link to their work
    public class AssessmentsApiController : StudentApiController
    {
        private readonly IStudentPortalService _portal;

        public AssessmentsApiController(IStudentPortalService portal)
        {
            _portal = portal;
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
        public async Task<IActionResult> Submit(int id, SubmissionRequest request)
        {
            var result = await _portal.SubmitAsync(StudentId, id, request.Link);
            return result.Outcome switch
            {
                SubmitOutcome.NotFound => NotFoundError(),
                SubmitOutcome.BadLink => Error(StatusCodes.Status400BadRequest, "invalid_link", StudentPortalService.BadLinkMessage),
                _ => Ok(await FindAsync(id))
            };
        }

        private async Task<AssessmentDto?> FindAsync(int id) =>
            (await _portal.AssessmentsAsync(StudentId)).Where(a => a.AssessmentId == id).Select(ApiMap.Assessment).FirstOrDefault();

        // Another module's assessment looks exactly like one that doesn't exist
        private static ObjectResult NotFoundError() =>
            Error(StatusCodes.Status404NotFound, "not_found", "That assessment isn't in any of your modules.");
    }
}
