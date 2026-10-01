using System.Security.Claims;

namespace ISMLTS_WebApp_.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        // The website's cookie uses the long .NET claim names; the app's access tokens use "sub" and "role"
        public static int? GetUserId(this ClaimsPrincipal user) =>
            int.TryParse((user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub"))?.Value, out var id) ? id : null;

        // Each account has exactly one role (Admin, Lecturer or Student)
        public static string GetRole(this ClaimsPrincipal user) =>
            (user.FindFirst(ClaimTypes.Role) ?? user.FindFirst("role"))?.Value ?? string.Empty;
    }
}
