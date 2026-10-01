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

        public ProfileController(IAccountService accountService, IStudentRepository studentRepository, IModuleRepository moduleRepository)
        {
            _accountService = accountService;
            _studentRepository = studentRepository;
            _moduleRepository = moduleRepository;
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
                    this.Toast("Your password has been changed.");
                    return RedirectToAction(nameof(Index));
                }
                ModelState.AddModelError($"Password.{error.Field}", error.Message);
            }

            var model = await BuildAsync(account);
            return View(nameof(Index), model);
        }

        private async Task<UserAccount?> CurrentAccountAsync() =>
            User.GetUserId() is int id ? await _accountService.FindAsync(User.GetRole(), id) : null;

        private async Task<ProfileViewModel> BuildAsync(UserAccount account)
        {
            var model = new ProfileViewModel { Role = account.Role, DisplayName = account.DisplayName, Login = account.Login };
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
