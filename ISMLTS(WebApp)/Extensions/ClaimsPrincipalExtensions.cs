using System.Security.Claims;

namespace ISMLTS_WebApp_.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static int? GetUserId(this ClaimsPrincipal user) =>
            int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        // Each account has exactly one role (Admin, Lecturer or Student)
        public static string GetRole(this ClaimsPrincipal user) =>
            user.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    }
}
