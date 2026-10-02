using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    // Admins check the live site's settings and connections after a deploy
    [Authorize(Roles = Roles.Admin)]
    public class SiteCheckController : Controller
    {
        private readonly ISiteCheckService _siteCheck;

        public SiteCheckController(ISiteCheckService siteCheck)
        {
            _siteCheck = siteCheck;
        }

        public async Task<IActionResult> Index() => View(await _siteCheck.RunAsync(HttpContext));
    }
}
