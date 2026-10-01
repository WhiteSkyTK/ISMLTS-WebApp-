using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Controllers.Api
{
    // Every app endpoint except log-in: a student's bearer token (never the website's cookie), JSON in and out, /api/v1/...
    // Bearer tokens aren't sent by browsers on their own, so these JSON endpoints don't need antiforgery tokens,
    // and request bodies are small records with only the fields the app may set.
    [ApiController]
    [Route("api/v1")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Roles.Student)]
    public abstract class StudentApiController : ControllerBase
    {
        // From the token's "sub" claim; every query below is limited to this student
        protected int StudentId => User.GetUserId() ?? 0;

        protected static ObjectResult Error(int status, string code, string message) => ApiProblems.Result(status, code, message);
    }
}
