using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Models.Api;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers.Api
{
    // Questions to a module's lecturer
    public class TicketsApiController : StudentApiController
    {
        private readonly ITicketRepository _tickets;
        private readonly IStudentPortalService _portal;

        public TicketsApiController(ITicketRepository tickets, IStudentPortalService portal)
        {
            _tickets = tickets;
            _portal = portal;
        }

        [HttpGet("tickets")]
        [ProducesResponseType<List<TicketDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> List() =>
            Ok((await _tickets.GetByStudentAsync(StudentId)).Select(ApiMap.Ticket).ToList());

        [HttpPost("tickets")]
        [ProducesResponseType<TicketDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create(TicketRequest request)
        {
            var result = await _portal.RaiseTicketAsync(StudentId, request.ModuleId, request.Subject, request.Description);
            if (result.Ticket == null)
            {
                var code = result.Field switch
                {
                    nameof(Ticket.ModuleId) => "invalid_module",
                    nameof(Ticket.Subject) => "invalid_subject",
                    _ => "invalid_description"
                };
                return Error(StatusCodes.Status400BadRequest, code, result.Error ?? "Check your question and try again.");
            }
            return StatusCode(StatusCodes.Status201Created, ApiMap.Ticket(result.Ticket));
        }
    }
}
