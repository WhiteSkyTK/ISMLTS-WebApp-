using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Controllers
{
    public class TicketsController : Controller
    {
        private readonly ITicketRepository _ticketRepository;
        private readonly IStudentRepository _studentRepository;

        public TicketsController(ITicketRepository ticketRepository, IStudentRepository studentRepository)
        {
            _ticketRepository = ticketRepository;
            _studentRepository = studentRepository;
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Index()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (idClaim == null || !int.TryParse(idClaim, out var lecturerId)) return Forbid();
            return View(await _ticketRepository.GetByLecturerAsync(lecturerId));
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Respond(int id)
        {
            var ticket = await _ticketRepository.GetByIdWithDetailsAsync(id);
            if (ticket == null) return NotFound();
            return View(ticket);
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Respond(int id, string status, string? lecturerResponse)
        {
            var ticket = await _ticketRepository.GetByIdAsync(id);
            if (ticket == null) return NotFound();

            ticket.Status = status;
            ticket.LecturerResponse = lecturerResponse;
            ticket.DateResolved = status == "Resolved" ? DateTime.Now : null;

            _ticketRepository.Update(ticket);
            await _ticketRepository.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyTickets()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (idClaim == null || !int.TryParse(idClaim, out var studentId)) return Forbid();
            return View(await _ticketRepository.GetByStudentAsync(studentId));
        }

        [Authorize(Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (idClaim == null || !int.TryParse(idClaim, out var studentId)) return Forbid();

            var student = await _studentRepository.GetByIdWithModulesAsync(studentId);
            ViewBag.Modules = student?.Modules
                .Select(m => new { m.ModuleId, Display = $"{m.Code} - {m.Name}" })
                .Cast<object>().ToList() ?? new List<object>();
            return View();
        }

        [Authorize(Roles = "Student")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ModuleId,Subject,Description")] Ticket ticket)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (idClaim == null || !int.TryParse(idClaim, out var studentId)) return Forbid();

            if (!ModelState.IsValid)
            {
                var student = await _studentRepository.GetByIdWithModulesAsync(studentId);
                ViewBag.Modules = student?.Modules
                    .Select(m => new { m.ModuleId, Display = $"{m.Code} - {m.Name}" })
                    .Cast<object>().ToList() ?? new List<object>();
                return View(ticket);
            }

            ticket.StudentId = studentId;
            await _ticketRepository.AddAsync(ticket);
            await _ticketRepository.SaveChangesAsync();
            return RedirectToAction(nameof(MyTickets));
        }
    }
}