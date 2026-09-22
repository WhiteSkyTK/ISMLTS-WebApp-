using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var admin = await _adminRepository.GetByUsernameAsync(model.EmailOrUsername);
            if (admin != null && BCrypt.Net.BCrypt.Verify(model.Password, admin.PasswordHash))
            {
                await SignInAsync(admin.AdminId.ToString(), admin.Username, "Admin");
                return RedirectToAction("Index", "Home");
            }

            var lecturer = await _lecturerRepository.GetByEmailAsync(model.EmailOrUsername);
            if (lecturer != null && BCrypt.Net.BCrypt.Verify(model.Password, lecturer.PasswordHash))
            {
                await SignInAsync(lecturer.LecturerId.ToString(), lecturer.FullName, "Lecturer");
                return RedirectToAction("Index", "Home");
            }

            var student = await _studentRepository.GetByEmailAsync(model.EmailOrUsername);
            if (student != null && BCrypt.Net.BCrypt.Verify(model.Password, student.PasswordHash))
            {
                await SignInAsync(student.StudentId.ToString(), student.FullName, "Student");
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        private async Task SignInAsync(string id, string name, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Name, name),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }

        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied() => View();
    }
}