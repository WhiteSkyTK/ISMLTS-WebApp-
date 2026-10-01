using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models.Api;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers.Api
{
    // Scanning in from the app: the same checks as the website (open session, enrolled, campus network or location)
    public class AttendanceApiController : StudentApiController
    {
        private readonly IStudentPortalService _portal;

        public AttendanceApiController(IStudentPortalService portal)
        {
            _portal = portal;
        }

        [HttpPost("attendance/scan")]
        [ProducesResponseType<ScanResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Scan(ScanRequest request)
        {
            var result = await _portal.ScanAsync(StudentId, request.Code, HttpContext.Connection.RemoteIpAddress,
                request.Latitude, request.Longitude, request.Accuracy);
            if (result.Present) return Ok(new ScanResponse(result.Code, result.Message, result.Module?.Code));

            var status = result.Code switch
            {
                ScanCodes.UnknownCode => StatusCodes.Status404NotFound,
                ScanCodes.NotEnrolled => StatusCodes.Status403Forbidden,
                ScanCodes.AlreadyPresent => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };
            return Error(status, result.Code, result.Message);
        }
    }
}
