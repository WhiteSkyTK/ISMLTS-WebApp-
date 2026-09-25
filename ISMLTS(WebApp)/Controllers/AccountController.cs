using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Controllers
{
    public class AccountController : Controller
    {
        private readonly IStudentRepository _studentRepository;
        private readonly ILecturerRepository _lecturerRepository;
        private readonly IAdminRepository _adminRepository;

        public AccountController(
            IStudentRepository studentRepository,
            ILecturerRepository lecturerRepository,
            IAdminRepository adminRepository)
        {
            _studentRepository = studentRepository;
            _lecturerRepository = lecturerRepository;
            _adminRepository = adminRepository;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var account = await FindAccountAsync(model.EmailOrUsername.Trim(), model.Password);
            if (account == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, account.Value.Id),
                new(ClaimTypes.Name, account.Value.Name),
                new(ClaimTypes.Role, account.Value.Role)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            // Only follow return URLs on this site (blocks open-redirect attacks)
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied() => View();

        private async Task<(string Id, string Name, string Role)?> FindAccountAsync(string login, string password)
        {
            var admin = await _adminRepository.GetByUsernameAsync(login);
            if (admin != null && PasswordMatches(password, admin.PasswordHash))
                return (admin.AdminId.ToString(), admin.Username, "Admin");

            var lecturer = await _lecturerRepository.GetByEmailAsync(login);
            if (lecturer != null && PasswordMatches(password, lecturer.PasswordHash))
                return (lecturer.LecturerId.ToString(), lecturer.FullName, "Lecturer");

            var student = await _studentRepository.GetByEmailAsync(login);
            if (student != null && PasswordMatches(password, student.PasswordHash))
                return (student.StudentId.ToString(), student.FullName, "Student");

            return null;
        }

        // Accounts saved before hashing was added hold text BCrypt can't parse: treat as a failed login
        private static bool PasswordMatches(string password, string hash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or ArgumentException)
            {
                return false;
            }
        }
    }
}