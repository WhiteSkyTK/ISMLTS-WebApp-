using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        public const string LoginRateLimitPolicy = "login";

        // A short-lived cookie for someone who passed the password step and still owes an authenticator code
        public const string TwoFactorScheme = "TwoFactorPending";

        private readonly IAccountService _accountService;
        private readonly IQrCodeService _qrCodeService;
        private readonly MicrosoftSignInOptions _microsoft;

        public AccountController(IAccountService accountService, IQrCodeService qrCodeService, Microsoft.Extensions.Options.IOptions<MicrosoftSignInOptions> microsoft)
        {
            _accountService = accountService;
            _qrCodeService = qrCodeService;
            _microsoft = microsoft.Value;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (Request.Query.ContainsKey(MicrosoftSignIn.FailedQuery))
                ModelState.AddModelError(string.Empty, "Signing in with Microsoft didn't finish. Try again, or log in with your password.");
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

            if (account.Entity.TwoFactorEnabled)
            {
                await StartPendingSignInAsync(account);
                return RedirectToAction(nameof(TwoFactor), new { returnUrl });
            }
            if (_accountService.MustSetUpTwoFactor(account))
            {
                await StartPendingSignInAsync(account);
                return RedirectToAction(nameof(SetUpTwoFactor), new { returnUrl });
            }

            await SignInAsync(account, passedTwoFactor: false);
            return RedirectAfterSignIn(returnUrl);
        }

        // Off to Microsoft; it comes back to MicrosoftDone. Only shown and allowed when Authentication:Microsoft is set.
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(LoginRateLimitPolicy)]
        public IActionResult MicrosoftLogin(string? returnUrl = null)
        {
            if (!_microsoft.IsConfigured) return NotFound();
            var properties = new AuthenticationProperties { RedirectUri = Url.Action(nameof(MicrosoftDone), new { returnUrl = SafeReturnUrl(returnUrl) }) };
            return Challenge(properties, MicrosoftSignIn.Scheme);
        }

        // Microsoft vouched for the person; sign in the student or lecturer with that college email. Accounts with an
        // authenticator app still enter their code, exactly as after a password.
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> MicrosoftDone(string? returnUrl = null)
        {
            if (!_microsoft.IsConfigured) return NotFound();
            var external = await HttpContext.AuthenticateAsync(MicrosoftSignIn.ExternalScheme);
            await HttpContext.SignOutAsync(MicrosoftSignIn.ExternalScheme);
            if (!external.Succeeded) return RedirectToAction(nameof(Login), new { returnUrl, microsoft = "failed" });

            var login = MicrosoftSignIn.Login(external.Principal);
            var account = await _accountService.FindByCollegeEmailAsync(login);
            if (account == null)
            {
                ViewData["ReturnUrl"] = returnUrl;
                ModelState.AddModelError(string.Empty, $"No student or lecturer account in ISMLTS uses {login ?? "that Microsoft account"}. Log in with your password, or ask the admin office.");
                return View(nameof(Login), new LoginViewModel());
            }

            if (account.Entity.TwoFactorEnabled)
            {
                await StartPendingSignInAsync(account);
                return RedirectToAction(nameof(TwoFactor), new { returnUrl });
            }
            await SignInAsync(account, passedTwoFactor: false);
            return RedirectAfterSignIn(returnUrl);
        }

        // Second step for accounts with an authenticator app
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> TwoFactor(string? returnUrl = null)
        {
            if (await PendingAccountAsync() == null) return RedirectToAction(nameof(Login), new { returnUrl });
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(LoginRateLimitPolicy)]
        public async Task<IActionResult> TwoFactor(string? code, string? returnUrl = null)
        {
            var account = await PendingAccountAsync();
            if (account == null) return RedirectToAction(nameof(Login), new { returnUrl });
            ViewData["ReturnUrl"] = returnUrl;

            var check = await _accountService.CheckTwoFactorAsync(account, code);
            if (check == TwoFactorCheck.Wrong)
            {
                ModelState.AddModelError(string.Empty, "That code didn't work. Use the newest code from your app, or one of your recovery codes.");
                return View();
            }

            await HttpContext.SignOutAsync(TwoFactorScheme);
            await SignInAsync(account, passedTwoFactor: true);
            if (check == TwoFactorCheck.RecoveryCode)
            {
                var left = Services.TwoFactor.RecoveryCodesLeft(account.Entity.TwoFactorRecoveryCodes);
                this.Toast($"You used a recovery code ({left} left). Make new ones on your profile if you're running low.", ToastTypes.Info);
            }
            return RedirectAfterSignIn(returnUrl);
        }

        // Admins without an authenticator app set one up before they get in
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> SetUpTwoFactor(string? returnUrl = null)
        {
            var account = await PendingAccountAsync();
            if (account == null) return RedirectToAction(nameof(Login), new { returnUrl });
            if (account.Entity.TwoFactorEnabled) return RedirectToAction(nameof(TwoFactor), new { returnUrl });

            return View(await SetupModelAsync(account, returnUrl, error: null));
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(LoginRateLimitPolicy)]
        public async Task<IActionResult> SetUpTwoFactor(string? code, string? returnUrl = null)
        {
            var account = await PendingAccountAsync();
            if (account == null) return RedirectToAction(nameof(Login), new { returnUrl });
            if (account.Entity.TwoFactorEnabled) return RedirectToAction(nameof(TwoFactor), new { returnUrl });

            var recoveryCodes = await _accountService.EnableTwoFactorAsync(account, code);
            if (recoveryCodes == null)
            {
                return View(await SetupModelAsync(account, returnUrl, "That code didn't match. Check the time on your phone is set automatically, then try the newest code."));
            }

            await HttpContext.SignOutAsync(TwoFactorScheme);
            await SignInAsync(account, passedTwoFactor: true);
            return View("RecoveryCodes", new RecoveryCodesViewModel { Codes = recoveryCodes, ContinueUrl = SafeReturnUrl(returnUrl) });
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

        private async Task<TwoFactorSetupViewModel> SetupModelAsync(UserAccount account, string? returnUrl, string? error)
        {
            var secret = await _accountService.StartTwoFactorSetupAsync(account);
            return new TwoFactorSetupViewModel
            {
                QrDataUri = _qrCodeService.ToPngDataUri(Services.TwoFactor.OtpAuthUri(secret, account.Login)),
                Secret = Services.TwoFactor.Grouped(secret),
                Login = account.Login,
                FormController = "Account",
                FormAction = nameof(SetUpTwoFactor),
                ReturnUrl = returnUrl,
                Error = error
            };
        }

        private Task StartPendingSignInAsync(UserAccount account) =>
            HttpContext.SignInAsync(TwoFactorScheme, AccountService.Principal(account, TwoFactorScheme, passedTwoFactor: false));

        private async Task<UserAccount?> PendingAccountAsync()
        {
            var pending = await HttpContext.AuthenticateAsync(TwoFactorScheme);
            if (!pending.Succeeded || pending.Principal.GetUserId() is not int id) return null;
            return await _accountService.FindAsync(pending.Principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty, id);
        }

        private Task SignInAsync(UserAccount account, bool passedTwoFactor) =>
            HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                AccountService.Principal(account, CookieAuthenticationDefaults.AuthenticationScheme, passedTwoFactor));

        // Only follow return URLs on this site (blocks open-redirect attacks)
        private string SafeReturnUrl(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Index", "Home") ?? "/";

        private IActionResult RedirectAfterSignIn(string? returnUrl) => LocalRedirect(SafeReturnUrl(returnUrl));
    }
}
