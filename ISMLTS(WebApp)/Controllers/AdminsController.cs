using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Controllers
{
    public class AdminsController : Controller
    {
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
            if (string.IsNullOrWhiteSpace(password))
                ModelState.AddModelError(string.Empty, "Password is required.");

            if (!ModelState.IsValid) return View(admin);

            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            await _adminRepository.AddAsync(admin);
            await _adminRepository.SaveChangesAsync();
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
            if (!ModelState.IsValid) return View(input);

            var admin = await _adminRepository.GetByIdAsync(id);
            if (admin == null) return NotFound();

            admin.Username = input.Username;
            admin.Role = input.Role;

            _adminRepository.Update(admin);
            await _adminRepository.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
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
    }
}