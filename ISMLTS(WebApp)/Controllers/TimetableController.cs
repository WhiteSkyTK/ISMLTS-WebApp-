using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    // A lecturer's weekly classes, for their own modules only
    [Authorize(Roles = Roles.Lecturer)]
    public class TimetableController : Controller
    {
        private readonly ITimetableRepository _slots;
        private readonly IModuleRepository _modules;

        public TimetableController(ITimetableRepository slots, IModuleRepository modules)
        {
            _slots = slots;
            _modules = modules;
        }

        public async Task<IActionResult> Index() => View(await PageAsync(new TimetableSlot
        {
            Day = DayOfWeek.Monday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(9, 30)
        }));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ModuleId,Day,StartTime,EndTime,Venue,OnDate")] TimetableSlot slot)
        {
            var module = await _modules.GetByIdAsync(slot.ModuleId);
            if (module == null || module.LecturerId != User.GetUserId()) return NotFound();

            slot.Venue = slot.Venue?.Trim() ?? string.Empty;
            // An extra class happens once, on its own date
            if (slot.OnDate is DateTime once) { slot.OnDate = once.Date; slot.Day = once.DayOfWeek; }
            if (Timetable.Problem(slot) is { } problem) ModelState.AddModelError(problem.Field, problem.Message);
            if (!ModelState.IsValid) return View(nameof(Index), await PageAsync(slot));

            await _slots.AddAsync(slot);
            await _slots.SaveChangesAsync();
            this.Toast($"{module.Code} on {Timetable.When(slot)} {Timetable.Times(slot)} was added.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var slot = await _slots.GetWithModuleAsync(id);
            if (slot?.Module == null || slot.Module.LecturerId != User.GetUserId()) return NotFound();

            _slots.Delete(slot);
            await _slots.SaveChangesAsync();
            this.Toast($"{slot.Module.Code} on {Timetable.When(slot)} {Timetable.Times(slot)} was removed.");
            return RedirectToAction(nameof(Index));
        }

        private async Task<TimetablePageModel> PageAsync(TimetableSlot form)
        {
            var modules = (await _modules.GetByLecturerAsync(User.GetUserId() ?? 0)).OrderBy(m => m.Code).ToList();
            return new TimetablePageModel
            {
                Modules = modules,
                Slots = await _slots.GetByModulesAsync(modules.Select(m => m.ModuleId).ToList()),
                Form = form
            };
        }
    }
}
