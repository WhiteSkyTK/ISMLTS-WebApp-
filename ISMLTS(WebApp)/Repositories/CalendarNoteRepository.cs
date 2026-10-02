using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public class CalendarNoteRepository : Repository<CalendarNote>, ICalendarNoteRepository
    {
        public CalendarNoteRepository(ApplicationDbContext context) : base(context) { }

        public async Task<List<CalendarNote>> GetForUserAsync(string role, int userId, DateTime from, DateTime to) =>
            await _dbSet.AsNoTracking()
                .Where(n => n.Role == role && n.UserId == userId && n.Date >= from && n.Date < to)
                .OrderBy(n => n.Date)
                .ToListAsync();

        public async Task<CalendarNote?> GetOwnedAsync(int noteId, string role, int userId) =>
            await _dbSet.FirstOrDefaultAsync(n => n.NoteId == noteId && n.Role == role && n.UserId == userId);

        public async Task<List<CalendarNote>> GetUnsentRemindersAsync(DateTime upToDate) =>
            await _dbSet.Where(n => n.Remind && n.RemindedAt == null && !n.IsDone && n.Date <= upToDate).ToListAsync();
    }
}
