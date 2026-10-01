using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Filters
{
    // Keeps a signed-in user on their profile until their account is in order:
    // a temporary password from an admin reset must be replaced, and admins whose session didn't pass the
    // authenticator step (for example one started before two-factor was required) must set it up
    public class AccountSetupFilter : IAuthorizationFilter
    {
        private static readonly HashSet<string> AllowedControllers = new(StringComparer.OrdinalIgnoreCase) { "Profile", "Account", "Status" };
        private readonly TwoFactorOptions _options;

        public AccountSetupFilter(IOptions<TwoFactorOptions> options)
        {
            _options = options.Value;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            // The app's API refuses temporary passwords and non-students at log-in instead
            if (user.Identity?.IsAuthenticated != true || ApiProblems.IsApiRequest(context.HttpContext)) return;
            if (context.RouteData.Values["controller"] is string controller && AllowedControllers.Contains(controller)) return;

            if (user.HasClaim(AccountService.ChangePasswordClaim, "true"))
            {
                context.Result = new RedirectToActionResult("Index", "Profile", null);
            }
            else if (_options.RequiredForAdmins && user.IsInRole(Roles.Admin) && !user.HasClaim(AccountService.TwoFactorClaim, "true"))
            {
                context.Result = new RedirectToActionResult("TwoFactor", "Profile", null);
            }
        }
    }
}
