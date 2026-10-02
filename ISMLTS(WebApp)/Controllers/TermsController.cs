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

        public TermsController(ITermRepository terms)
        {
            _terms = terms;
        }

        public async Task<IActionResult> Index()
        {
            var terms = await _terms.GetOrderedAsync();
            ViewBag.CurrentTermId = Terms.Current(terms, DateTime.Today)?.TermId;
            return View(terms);
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
