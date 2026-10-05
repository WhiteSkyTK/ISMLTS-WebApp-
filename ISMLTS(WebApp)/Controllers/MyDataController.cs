using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    // A student downloads everything ISMLTS holds about them (their own records only)
    [Authorize(Roles = Roles.Student)]
    public class MyDataController : Controller
    {
        private readonly IMyDataService _data;

        public MyDataController(IMyDataService data)
        {
            _data = data;
        }

        public async Task<IActionResult> Download()
        {
            if (User.GetUserId() is not int studentId) return Forbid();
            var zip = await _data.ExportAsync(studentId);
            if (zip == null) return NotFound();

            Response.Headers.CacheControl = "no-store";
            return File(zip, "application/zip", $"my-ismlts-data-{DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.zip");
        }
    }
}
