using Microsoft.AspNetCore.Mvc;

namespace ISMLTS_WebApp_.Extensions
{
    // Every API error is a JSON problem (RFC 9457): "title" is a sentence the app can show,
    // "code" is a fixed word the app can switch on, e.g. {"status":401,"title":"...","code":"two_factor_required"}
    public static class ApiProblems
    {
        public const string ContentType = "application/problem+json";

        public static bool IsApiRequest(HttpContext http) => http.Request.Path.StartsWithSegments("/api");

        public static ProblemDetails Problem(int status, string code, string message)
        {
            var problem = new ProblemDetails { Status = status, Title = message };
            problem.Extensions["code"] = code;
            return problem;
        }

        public static ObjectResult Result(int status, string code, string message) =>
            new(Problem(status, code, message)) { StatusCode = status, ContentTypes = { ContentType } };

        public static Task WriteAsync(HttpContext http, int status, string code, string message)
        {
            http.Response.StatusCode = status;
            return http.Response.WriteAsJsonAsync(Problem(status, code, message), options: null, contentType: ContentType, cancellationToken: http.RequestAborted);
        }
    }
}
