using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public AdminsController(IAdminRepository adminRepository)
        {
            _adminRepository = adminRepository;
        }

        public async Task<IActionResult> Index() => View(await _adminRepository.GetAllAsync());

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
            return await TrySaveAsync(admin.Username, 0) ? RedirectToAction(nameof(Index)) : View(admin);
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
            return await TrySaveAsync(input.Username, id) ? RedirectToAction(nameof(Index)) : View(input);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var admin = await _adminRepository.GetByIdAsync(id);
            if (admin == null) return NotFound();
            return View(admin);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var admin = await _adminRepository.GetByIdAsync(id);
            if (admin != null)
            {
                _adminRepository.Delete(admin);
                await _adminRepository.SaveChangesAsync();
            }
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
