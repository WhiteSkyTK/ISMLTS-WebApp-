using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers
{
    [Authorize]
    public class TicketsController : Controller
    {
        private static readonly string[] Statuses = { "Open", "In Progress", "Resolved" };
        private const int MaxResponseLength = 1000;

        private readonly ITicketRepository _ticketRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly INotificationService _notifications;
        private readonly IStudentPortalService _portal;

        public TicketsController(ITicketRepository ticketRepository, IStudentRepository studentRepository, INotificationService notifications, IStudentPortalService portal)
        {
            _ticketRepository = ticketRepository;
            _studentRepository = studentRepository;
            _notifications = notifications;
            _portal = portal;
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Index()
        {
            if (User.GetUserId() is not int lecturerId) return Forbid();
            return View(await _ticketRepository.GetByLecturerAsync(lecturerId));
        }

        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> Respond(int id)
        {
            var ticket = await GetOwnedTicketAsync(id);
            if (ticket == null) return NotFound();
            return View(ticket);
        }

        [Authorize(Roles = "Lecturer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Respond(int id, string status, string? lecturerResponse)
        {
            var ticket = await GetOwnedTicketAsync(id);
            if (ticket == null) return NotFound();
            if (!Statuses.Contains(status)) return BadRequest();

            if (lecturerResponse?.Length > MaxResponseLength)
            {
                ModelState.AddModelError(nameof(Ticket.LecturerResponse), $"Keep the response to {MaxResponseLength} characters or fewer.");
                ticket.LecturerResponse = lecturerResponse; // redisplay what was typed; never saved
                return View(ticket);
            }

            ticket.Status = status;
            ticket.LecturerResponse = lecturerResponse;
            ticket.DateResolved = status == "Resolved" ? DateTime.UtcNow : null;

            _ticketRepository.Update(ticket);
            await _ticketRepository.SaveChangesAsync();
            await _notifications.TicketAnsweredAsync(ticket, ticket.Module!);
            this.Toast($"Your reply to {ticket.Student?.FullName} was saved ({status}).");
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyTickets()
        {
            if (User.GetUserId() is not int studentId) return Forbid();
            return View(await _ticketRepository.GetByStudentAsync(studentId));
        }

        [Authorize(Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (User.GetUserId() is not int studentId) return Forbid();

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
            if (User.GetUserId() is not int studentId) return Forbid();

            if (ModelState.IsValid)
            {
                var result = await _portal.RaiseTicketAsync(studentId, ticket.ModuleId, ticket.Subject, ticket.Description);
                if (result.Ticket != null)
                {
                    this.Toast("Your ticket was sent to your lecturer.");
                    return RedirectToAction(nameof(MyTickets));
                }
                ModelState.AddModelError(result.Field ?? string.Empty, result.Error ?? "Your ticket could not be sent.");
            }

            var student = await _studentRepository.GetByIdWithModulesAsync(studentId);
            ViewBag.Modules = student?.Modules
                .Select(m => new { m.ModuleId, Display = $"{m.Code} - {m.Name}" })
                .Cast<object>().ToList() ?? new List<object>();
            return View(ticket);
        }

        // A ticket on another lecturer's module looks exactly like one that doesn't exist
        private async Task<Ticket?> GetOwnedTicketAsync(int id)
        {
            var ticket = await _ticketRepository.GetByIdWithDetailsAsync(id);
            return ticket?.Module != null && ticket.Module.LecturerId == User.GetUserId() ? ticket : null;
        }
    }
}