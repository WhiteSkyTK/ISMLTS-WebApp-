using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    // Admins set the dates of each term; the latest one that has started is "current" across the site
    [Authorize(Roles = Roles.Admin)]
    public class TermsController : Controller
    {
        private readonly ITermRepository _terms;
        private readonly ICollegeDateRepository _dates;

        public TermsController(ITermRepository terms, ICollegeDateRepository dates)
        {
            _terms = terms;
            _dates = dates;
        }

        public async Task<IActionResult> Index() => View(await PageAsync(null));

        // Holidays, exam and assignment weeks, breaks and closing dates show on everyone's calendar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDate([Bind("Title,Kind,StartDate,EndDate")] CollegeDate date)
        {
            date.Title = date.Title?.Trim() ?? string.Empty;
            if (!CollegeDateKinds.IsValid(date.Kind)) ModelState.AddModelError(nameof(CollegeDate.Kind), "Pick what kind of date this is.");
            if (date.EndDate.Date < date.StartDate.Date) ModelState.AddModelError(nameof(CollegeDate.EndDate), "The last day must be on or after the first day.");
            if (!ModelState.IsValid) return View(nameof(Index), await PageAsync(date));

            date.StartDate = date.StartDate.Date;
            date.EndDate = date.EndDate.Date;
            await _dates.AddAsync(date);
            await _dates.SaveChangesAsync();
            this.Toast($"{date.Title} was added to the calendar.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDate(int id)
        {
            var date = await _dates.GetByIdAsync(id);
            if (date == null) return NotFound();
            _dates.Delete(date);
            await _dates.SaveChangesAsync();
            this.Toast($"{date.Title} was removed from the calendar.");
            return RedirectToAction(nameof(Index));
        }

        private async Task<TermsPageModel> PageAsync(CollegeDate? form)
        {
            var terms = await _terms.GetOrderedAsync();
            return new TermsPageModel
            {
                Terms = terms,
                CurrentTermId = Terms.Current(terms, DateTime.Today)?.TermId,
                Dates = await _dates.GetOrderedAsync(),
                NewDate = form ?? new CollegeDate { StartDate = DateTime.Today, EndDate = DateTime.Today }
            };
        }

        [HttpGet]
        public IActionResult Create()
        {
            var today = DateTime.Today;
            var secondHalf = today.Month >= 7;
            return View(new Term
            {
                Name = $"{today.Year} {(secondHalf ? "Term 2" : "Term 1")}",
                Code = secondHalf ? CourseTerms.Term2 : CourseTerms.Term1,
                StartDate = today,
                EndDate = today.AddDays(120)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Code,StartDate,EndDate")] Term term)
        {
            await ValidateAsync(term);
            if (!ModelState.IsValid) return View(term);

            await _terms.AddAsync(term);
            await _terms.SaveChangesAsync();
            this.Toast($"{term.Name} was added.");
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var term = await _terms.GetByIdAsync(id);
            return term == null ? NotFound() : View(term);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("TermId,Name,Code,StartDate,EndDate")] Term input)
        {
            if (id != input.TermId) return NotFound();
            var term = await _terms.GetByIdAsync(id);
            if (term == null) return NotFound();

            await ValidateAsync(input);
            if (!ModelState.IsValid) return View(input);

            term.Name = input.Name.Trim();
            term.Code = input.Code;
            term.StartDate = input.StartDate.Date;
            term.EndDate = input.EndDate.Date;
            _terms.Update(term);
            await _terms.SaveChangesAsync();
            this.Toast($"Changes to {term.Name} were saved.");
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var term = await _terms.GetByIdAsync(id);
            if (term == null) return NotFound();

            _terms.Delete(term);
            await _terms.SaveChangesAsync();
            this.Toast($"{term.Name} was deleted.");
            return RedirectToAction(nameof(Index));
        }

        private async Task ValidateAsync(Term term)
        {
            term.Name = term.Name?.Trim() ?? string.Empty;
            term.StartDate = term.StartDate.Date;
            term.EndDate = term.EndDate.Date;
            if (Terms.Problem(term) is { } problem) ModelState.AddModelError(problem.Field, problem.Message);
            if (term.Name.Length > 0 && await _terms.NameExistsAsync(term.Name, term.TermId))
                ModelState.AddModelError(nameof(Term.Name), "There is already a term with that name.");
        }
    }
}
