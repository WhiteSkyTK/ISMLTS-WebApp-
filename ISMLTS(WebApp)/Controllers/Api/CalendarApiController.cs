using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Models.Api;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Controllers.Api
{
    // The student's calendar (classes, due and closing dates, term and college dates, their own notes) and their notes
    public class CalendarApiController : StudentApiController
    {
        public const int MaxDays = 92;

        private readonly ICalendarService _calendar;

        public CalendarApiController(ICalendarService calendar)
        {
            _calendar = calendar;
        }

        // from and to are dates (to not included); four weeks from today when left out, at most 92 days
        [HttpGet("calendar")]
        [ProducesResponseType<List<CalendarEntryDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Get(DateOnly? from, DateOnly? to)
        {
            var start = (from ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
            var end = (to ?? DateOnly.FromDateTime(start.AddDays(28))).ToDateTime(TimeOnly.MinValue);
            if (end <= start || (end - start).TotalDays > MaxDays)
                return Error(StatusCodes.Status400BadRequest, "invalid_range", $"Ask for 1 to {MaxDays} days: \"to\" must be after \"from\".");
            return Ok((await _calendar.ForUserAsync(Roles.Student, StudentId, start, end)).Select(ApiMap.CalendarEntry).ToList());
        }

        [HttpPost("calendar/notes")]
        [ProducesResponseType<CalendarEntryDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AddNote(NoteRequest request)
        {
            var note = new CalendarNote
            {
                Date = request.Date?.ToDateTime(TimeOnly.MinValue) ?? default,
                Time = request.Time,
                Title = request.Title ?? string.Empty,
                Details = request.Details,
                Remind = request.Remind
            };
            if (await _calendar.AddNoteAsync(Roles.Student, StudentId, note) is { } problem)
                return Error(StatusCodes.Status400BadRequest, "invalid_note", problem.Message);
            return StatusCode(StatusCodes.Status201Created, ApiMap.CalendarEntry(ApiMap.NoteEntry(note)));
        }

        [HttpPut("calendar/notes/{id:int}/done")]
        [ProducesResponseType<CalendarEntryDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetDone(int id, NoteDoneRequest request)
        {
            var note = await _calendar.SetNoteDoneAsync(Roles.Student, StudentId, id, request.Done);
            return note == null ? NotFoundError() : Ok(ApiMap.CalendarEntry(ApiMap.NoteEntry(note)));
        }

        [HttpDelete("calendar/notes/{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteNote(int id) =>
            await _calendar.DeleteNoteAsync(Roles.Student, StudentId, id) == null ? NotFoundError() : NoContent();

        // Someone else's note looks exactly like one that doesn't exist
        private static ObjectResult NotFoundError() => Error(StatusCodes.Status404NotFound, "not_found", "That note isn't one of yours.");
    }
}
