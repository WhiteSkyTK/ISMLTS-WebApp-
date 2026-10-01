using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    // Every role sees and changes only their own account here
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IAccountService _accountService;
        private readonly IStudentRepository _studentRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly IQrCodeService _qrCodeService;

        public ProfileController(IAccountService accountService, IStudentRepository studentRepository, IModuleRepository moduleRepository, IQrCodeService qrCodeService)
        {
            _accountService = accountService;
            _studentRepository = studentRepository;
            _moduleRepository = moduleRepository;
            _qrCodeService = qrCodeService;
        }

        public async Task<IActionResult> Index()
        {
            var account = await CurrentAccountAsync();
            return account == null ? NotFound() : View(await BuildAsync(account));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(AccountController.LoginRateLimitPolicy)] // slows guessing the current password
        public async Task<IActionResult> ChangePassword([Bind("CurrentPassword,NewPassword,ConfirmPassword")] ChangePasswordForm password)
        {
            var account = await CurrentAccountAsync();
            if (account == null) return NotFound();

            if (ModelState.IsValid)
            {
                var error = await _accountService.ChangePasswordAsync(account, password.CurrentPassword, password.NewPassword);
                if (error == null)
                {
                    // A fresh session cookie drops the "temporary password" reminder and keeps the two-factor step it passed
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                        AccountService.Principal(account, CookieAuthenticationDefaults.AuthenticationScheme, User.HasClaim(AccountService.TwoFactorClaim, "true")));
                    this.Toast("Your password has been changed.");
                    return RedirectToAction(nameof(Index));
                }
                ModelState.AddModelError($"Password.{error.Field}", error.Message);
            }

            return View(nameof(Index), await BuildAsync(account));
        }

        // Set up an authenticator app (admins are sent here until they have one)
        [HttpGet]
        public async Task<IActionResult> TwoFactor()
        {
            var account = await CurrentAccountAsync();
            if (account == null) return NotFound();
            if (account.Entity.TwoFactorEnabled)
            {
                if (User.HasClaim(AccountService.TwoFactorClaim, "true")) return RedirectToAction(nameof(Index));

                // Switched on from another session: this one has to log in again with a code
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                this.Toast("Log in again and enter the code from your authenticator app.", ToastTypes.Info);
                return RedirectToAction("Login", "Account");
            }

            ViewData["Required"] = _accountService.MustSetUpTwoFactor(account);
            return View(await SetupModelAsync(account, error: null));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(AccountController.LoginRateLimitPolicy)]
        public async Task<IActionResult> TwoFactor(string? code)
        {
            var account = await CurrentAccountAsync();
            if (account == null) return NotFound();
            if (account.Entity.TwoFactorEnabled) return RedirectToAction(nameof(Index));

            var recoveryCodes = await _accountService.EnableTwoFactorAsync(account, code);
            if (recoveryCodes == null)
            {
                ViewData["Required"] = _accountService.MustSetUpTwoFactor(account);
                return View(await SetupModelAsync(account, "That code didn't match. Check the time on your phone is set automatically, then try the newest code."));
            }

            // This session has now passed the authenticator step
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                AccountService.Principal(account, CookieAuthenticationDefaults.AuthenticationScheme, passedTwoFactor: true));
            return View("RecoveryCodes", new RecoveryCodesViewModel { Codes = recoveryCodes, ContinueUrl = Url.Action(nameof(Index)) ?? "/", ContinueText = "Back to your profile" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(AccountController.LoginRateLimitPolicy)]
        public async Task<IActionResult> TurnOffTwoFactor(string? currentPassword)
        {
            var account = await CurrentAccountAsync();
            if (account == null) return NotFound();

            if (!_accountService.CanTurnOffTwoFactor(account))
            {
                this.Toast("Admins have to keep the authenticator app switched on.", ToastTypes.Danger);
            }
            else if (!AccountService.PasswordMatches(currentPassword ?? string.Empty, account.Entity.PasswordHash))
            {
                this.Toast("Your password is not right, so two-factor sign-in is still on.", ToastTypes.Danger);
            }
            else
            {
                await _accountService.TurnOffTwoFactorAsync(account);
                this.Toast("Two-factor sign-in is off. You'll log in with just your password.", ToastTypes.Info);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(AccountController.LoginRateLimitPolicy)]
        public async Task<IActionResult> NewRecoveryCodes(string? currentPassword)
        {
            var account = await CurrentAccountAsync();
            if (account == null) return NotFound();
            if (!account.Entity.TwoFactorEnabled) return RedirectToAction(nameof(Index));

            if (!AccountService.PasswordMatches(currentPassword ?? string.Empty, account.Entity.PasswordHash))
            {
                this.Toast("Your password is not right, so your recovery codes haven't changed.", ToastTypes.Danger);
                return RedirectToAction(nameof(Index));
            }
            var codes = await _accountService.NewRecoveryCodesAsync(account);
            return View("RecoveryCodes", new RecoveryCodesViewModel { Codes = codes, ContinueUrl = Url.Action(nameof(Index)) ?? "/", ContinueText = "Back to your profile" });
        }

        private async Task<UserAccount?> CurrentAccountAsync() =>
            User.GetUserId() is int id ? await _accountService.FindAsync(User.GetRole(), id) : null;

        private async Task<TwoFactorSetupViewModel> SetupModelAsync(UserAccount account, string? error)
        {
            var secret = await _accountService.StartTwoFactorSetupAsync(account);
            return new TwoFactorSetupViewModel
            {
                QrDataUri = _qrCodeService.ToPngDataUri(Services.TwoFactor.OtpAuthUri(secret, account.Login)),
                Secret = Services.TwoFactor.Grouped(secret),
                Login = account.Login,
                FormController = "Profile",
                FormAction = nameof(TwoFactor),
                Error = error
            };
        }

        private async Task<ProfileViewModel> BuildAsync(UserAccount account)
        {
            var model = new ProfileViewModel
            {
                Role = account.Role,
                DisplayName = account.DisplayName,
                Login = account.Login,
                MustChangePassword = account.Entity.MustChangePassword,
                TwoFactorEnabled = account.Entity.TwoFactorEnabled,
                CanTurnOffTwoFactor = _accountService.CanTurnOffTwoFactor(account),
                RecoveryCodesLeft = Services.TwoFactor.RecoveryCodesLeft(account.Entity.TwoFactorRecoveryCodes)
            };
            if (account.Entity is Student)
            {
                var student = await _studentRepository.GetByIdWithModulesAsync(account.Id);
                model.Programme = student?.Programme;
                model.Modules = student?.Modules.OrderBy(m => m.Code).Select(m => $"{m.Code} - {m.Name}").ToList() ?? new();
            }
            else if (account.Entity is Lecturer)
            {
                model.Modules = (await _moduleRepository.GetByLecturerAsync(account.Id)).OrderBy(m => m.Code).Select(m => $"{m.Code} - {m.Name}").ToList();
            }
            return model;
        }
    }
}
