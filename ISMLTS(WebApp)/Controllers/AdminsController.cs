
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Data;

public class AdminsController : Controller
{
    private readonly ApplicationDbContext _context;

    public AdminsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: ADMINS
    public async Task<IActionResult> Index()    
    {
        return View(await _context.Admins.ToListAsync());
    }

    // GET: ADMINS/Details/5
    public async Task<IActionResult> Details(int? adminid)
    {
        if (adminid == null)
        {
            return NotFound();
        }

        var admin = await _context.Admins
            .FirstOrDefaultAsync(m => m.AdminId == adminid);
        if (admin == null)
        {
            return NotFound();
        }

        return View(admin);
    }

    // GET: ADMINS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: ADMINS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("AdminId,Username,PasswordHash,Role")] Admin admin)
    {
        if (ModelState.IsValid)
        {
            _context.Add(admin);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(admin);
    }

    // GET: ADMINS/Edit/5
    public async Task<IActionResult> Edit(int? adminid)
    {
        if (adminid == null)
        {
            return NotFound();
        }

        var admin = await _context.Admins.FindAsync(adminid);
        if (admin == null)
        {
            return NotFound();
        }
        return View(admin);
    }

    // POST: ADMINS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? adminid, [Bind("AdminId,Username,PasswordHash,Role")] Admin admin)
    {
        if (adminid != admin.AdminId)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(admin);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AdminExists(admin.AdminId))
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
        return View(admin);
    }

    // GET: ADMINS/Delete/5
    public async Task<IActionResult> Delete(int? adminid)
    {
        if (adminid == null)
        {
            return NotFound();
        }

        var admin = await _context.Admins
            .FirstOrDefaultAsync(m => m.AdminId == adminid);
        if (admin == null)
        {
            return NotFound();
        }

        return View(admin);
    }

    // POST: ADMINS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? adminid)
    {
        var admin = await _context.Admins.FindAsync(adminid);
        if (admin != null)
        {
            _context.Admins.Remove(admin);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool AdminExists(int? adminid)
    {
        return _context.Admins.Any(e => e.AdminId == adminid);
    }
}
