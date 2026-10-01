using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Filters
{
    // Admins whose session didn't pass the authenticator step (for example one that started before two-factor
    // was required) can only reach their profile, to set it up, until they do
    public class RequireAdminTwoFactorFilter : IAuthorizationFilter
    {
        private static readonly HashSet<string> AllowedControllers = new(StringComparer.OrdinalIgnoreCase) { "Profile", "Account", "Status" };
        private readonly TwoFactorOptions _options;

        public RequireAdminTwoFactorFilter(IOptions<TwoFactorOptions> options)
        {
            _options = options.Value;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (!_options.RequiredForAdmins || !user.IsInRole(Roles.Admin) || user.HasClaim(AccountService.TwoFactorClaim, "true")) return;
            if (context.RouteData.Values["controller"] is string controller && AllowedControllers.Contains(controller)) return;

            context.Result = new RedirectToActionResult("TwoFactor", "Profile", null);
        }
    }
}
