using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        public const string LoginRateLimitPolicy = "login";

        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(LoginRateLimitPolicy)]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var account = await _accountService.SignInAsync(model.EmailOrUsername.Trim(), model.Password);
            if (account == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }

            await SignInAsync(account);
            return RedirectAfterSignIn(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }

        private Task SignInAsync(UserAccount account)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, account.Id.ToString(CultureInfo.InvariantCulture)),
                new(ClaimTypes.Name, account.DisplayName),
                new(ClaimTypes.Role, account.Role)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }

        // Only follow return URLs on this site (blocks open-redirect attacks)
        private IActionResult RedirectAfterSignIn(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction("Index", "Home");
    }
}
