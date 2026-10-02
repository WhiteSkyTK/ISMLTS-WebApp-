using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface ICalendarNoteRepository : IRepository<CalendarNote>
    {
        Task<List<CalendarNote>> GetForUserAsync(string role, int userId, DateTime from, DateTime to);
        Task<CalendarNote?> GetOwnedAsync(int noteId, string role, int userId);

        // Notes with a reminder not sent yet, on or before this day
        Task<List<CalendarNote>> GetUnsentRemindersAsync(DateTime upToDate);
    }
}
