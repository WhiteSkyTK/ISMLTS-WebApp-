using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Controllers
{
    // Admins see who created or deleted accounts, reset passwords, turned off two-factor or changed enrolments
    [Authorize(Roles = Roles.Admin)]
    public class AuditController : Controller
    {
        private readonly IAuditRepository _entries;

        public AuditController(IAuditRepository entries)
        {
            _entries = entries;
        }

        public async Task<IActionResult> Index(string? q, int page = 1, string? action = null)
        {
            action = AuditActions.All.Contains(action) ? action : null;
            ViewData["SearchLabel"] = "Search by name or account";
            ViewData["ListFilters"] = new List<ListFilter>
            {
                new() { Name = "action", Label = "Action", AllText = "Every action", Selected = action, Options = AuditActions.All.Select(a => (a, a)).ToList() }
            };
            return View(await _entries.SearchAsync(q, page, action));
        }
    }
}
