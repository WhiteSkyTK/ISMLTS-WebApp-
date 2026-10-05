using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    // Admins help people who are locked out: a temporary password, or two-factor off after a lost phone
    [Authorize(Roles = Roles.Admin)]
    public class UserSecurityController : Controller
    {
        private readonly IAccountService _accountService;

        private readonly IAuditLog _audit;

        public UserSecurityController(IAccountService accountService, IAuditLog audit)
        {
            _audit = audit;
            _accountService = accountService;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string role, int id)
        {
            var account = await _accountService.FindAsync(role, id);
            if (account == null) return NotFound();

            var temporary = await _accountService.ResetToTemporaryPasswordAsync(account);
            await _audit.RecordAsync(User, AuditActions.PasswordReset, $"{account.Role} {account.DisplayName} ({account.Login})", "A temporary password was issued; app sign-ins were ended.");
            return View("PasswordReset", new PasswordResetViewModel
            {
                Name = account.DisplayName,
                Login = account.Login,
                TemporaryPassword = temporary,
                BackUrl = DetailsUrl(account)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TurnOffTwoFactor(string role, int id)
        {
            var account = await _accountService.FindAsync(role, id);
            if (account == null) return NotFound();

            await _accountService.TurnOffTwoFactorAsync(account);
            await _audit.RecordAsync(User, AuditActions.TwoFactorOff, $"{account.Role} {account.DisplayName} ({account.Login})");
            this.Toast(_accountService.MustSetUpTwoFactor(account)
                ? $"Two-factor sign-in is off for {account.DisplayName}. As an admin they'll set it up again at their next log-in."
                : $"Two-factor sign-in is off for {account.DisplayName}. They can log in with just their password.", ToastTypes.Info);
            return Redirect(DetailsUrl(account));
        }

        private string DetailsUrl(UserAccount account) => Url.Action("Details", account.Role switch
        {
            Roles.Admin => "Admins",
            Roles.Lecturer => "Lecturers",
            _ => "Students"
        }, new { id = account.Id }) ?? "/";
    }
}
