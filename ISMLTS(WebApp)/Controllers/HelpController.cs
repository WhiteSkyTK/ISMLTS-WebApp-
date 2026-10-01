using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISMLTS_WebApp_.Controllers
{
    // Signed-out visitors get the log-in questions; signed-in users also get the ones for their role
    [Authorize]
    public class HelpController : Controller
    {
        [AllowAnonymous]
        public IActionResult Index() => View();
    }
}
