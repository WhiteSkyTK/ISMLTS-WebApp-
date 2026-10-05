using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminsController : Controller
    {
        private const string UsernameTakenMessage = "That username is already taken.";

        private readonly IAdminRepository _adminRepository;

        private readonly IAuditLog _audit;

        public AdminsController(IAdminRepository adminRepository, IAuditLog audit)
        {
            _audit = audit;
            _adminRepository = adminRepository;
        }

        public async Task<IActionResult> Index(string? q, int page = 1, string? sort = null) =>
            View(await _adminRepository.SearchAsync(q, page, sort));

        public async Task<IActionResult> Details(int id)
        {
            var admin = await _adminRepository.GetByIdAsync(id);
            if (admin == null) return NotFound();
            return View(admin);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Username,Role")] Admin admin, string password)
        {
            if (!PasswordRules.IsLongEnough(password))
                ModelState.AddModelError(string.Empty, PasswordRules.TooShortMessage);
            await CheckUsernameIsFreeAsync(admin.Username, 0);

            if (!ModelState.IsValid) return View(admin);

            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            await _adminRepository.AddAsync(admin);
            if (!await TrySaveAsync(admin.Username, 0)) return View(admin);

            this.Toast($"Admin {admin.Username} was added.");
            await _audit.RecordAsync(User, AuditActions.AccountCreated, $"Admin {admin.Username}");
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var admin = await _adminRepository.GetByIdAsync(id);
            if (admin == null) return NotFound();
            return View(admin);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AdminId,Username,Role")] Admin input)
        {
            if (id != input.AdminId) return NotFound();
            await CheckUsernameIsFreeAsync(input.Username, id);
            if (!ModelState.IsValid) return View(input);

            var admin = await _adminRepository.GetByIdAsync(id);
            if (admin == null) return NotFound();

            admin.Username = input.Username;
            admin.Role = input.Role;

            _adminRepository.Update(admin);
            if (!await TrySaveAsync(input.Username, id)) return View(input);

            this.Toast($"Changes to {admin.Username} were saved.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var admin = await _adminRepository.GetByIdAsync(id);
            if (admin == null) return NotFound();

            // Deleting yourself would leave this session signed in as nobody
            if (admin.AdminId == User.GetUserId())
            {
                this.Toast("You can't delete your own account. Ask another admin to do it.", ToastTypes.Danger);
                return RedirectToAction(nameof(Index));
            }

            _adminRepository.Delete(admin);
            await _adminRepository.SaveChangesAsync();
            this.Toast($"Admin {admin.Username} was deleted.");
            await _audit.RecordAsync(User, AuditActions.AccountDeleted, $"Admin {admin.Username}");
            return RedirectToAction(nameof(Index));
        }

        private async Task CheckUsernameIsFreeAsync(string username, int adminId)
        {
            if (!string.IsNullOrWhiteSpace(username) && await _adminRepository.UsernameExistsAsync(username, adminId))
                ModelState.AddModelError(nameof(Admin.Username), UsernameTakenMessage);
        }

        // Two saves racing past the check above are stopped by the unique index
        private async Task<bool> TrySaveAsync(string username, int adminId)
        {
            try
            {
                await _adminRepository.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                if (!await _adminRepository.UsernameExistsAsync(username, adminId)) throw;
                ModelState.AddModelError(nameof(Admin.Username), UsernameTakenMessage);
                return false;
            }
        }
    }
}
