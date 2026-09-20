
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Data;

public class LecturersController : Controller
{
    private readonly ApplicationDbContext _context;

    public LecturersController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: LECTURERS
    public async Task<IActionResult> Index()    
    {
        return View(await _context.Lecturers.ToListAsync());
    }

    // GET: LECTURERS/Details/5
    public async Task<IActionResult> Details(int? lecturerid)
    {
        if (lecturerid == null)
        {
            return NotFound();
        }

        var lecturer = await _context.Lecturers
            .FirstOrDefaultAsync(m => m.LecturerId == lecturerid);
        if (lecturer == null)
        {
            return NotFound();
        }

        return View(lecturer);
    }

    // GET: LECTURERS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: LECTURERS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("LecturerId,FullName,Email,PasswordHash,Modules")] Lecturer lecturer)
    {
        if (ModelState.IsValid)
        {
            _context.Add(lecturer);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(lecturer);
    }

    // GET: LECTURERS/Edit/5
    public async Task<IActionResult> Edit(int? lecturerid)
    {
        if (lecturerid == null)
        {
            return NotFound();
        }

        var lecturer = await _context.Lecturers.FindAsync(lecturerid);
        if (lecturer == null)
        {
            return NotFound();
        }
        return View(lecturer);
    }

    // POST: LECTURERS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? lecturerid, [Bind("LecturerId,FullName,Email,PasswordHash,Modules")] Lecturer lecturer)
    {
        if (lecturerid != lecturer.LecturerId)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(lecturer);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LecturerExists(lecturer.LecturerId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(lecturer);
    }

    // GET: LECTURERS/Delete/5
    public async Task<IActionResult> Delete(int? lecturerid)
    {
        if (lecturerid == null)
        {
            return NotFound();
        }

        var lecturer = await _context.Lecturers
            .FirstOrDefaultAsync(m => m.LecturerId == lecturerid);
        if (lecturer == null)
        {
            return NotFound();
        }

        return View(lecturer);
    }

    // POST: LECTURERS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? lecturerid)
    {
        var lecturer = await _context.Lecturers.FindAsync(lecturerid);
        if (lecturer != null)
        {
            _context.Lecturers.Remove(lecturer);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool LecturerExists(int? lecturerid)
    {
        return _context.Lecturers.Any(e => e.LecturerId == lecturerid);
    }
}
