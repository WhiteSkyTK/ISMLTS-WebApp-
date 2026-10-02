using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public class TimetableRepository : Repository<TimetableSlot>, ITimetableRepository
    {
        public TimetableRepository(ApplicationDbContext context) : base(context) { }

        public async Task<List<TimetableSlot>> GetByModulesAsync(IReadOnlyCollection<int> moduleIds) =>
            (await _dbSet.AsNoTracking().Include(s => s.Module)
                .Where(s => moduleIds.Contains(s.ModuleId))
                .ToListAsync())
            // TimeOnly ordering is done here so SQLite (tests) and SQL Server agree
            .OrderBy(s => s.Day).ThenBy(s => s.StartTime).ToList();

        public async Task<TimetableSlot?> GetWithModuleAsync(int slotId) =>
            await _dbSet.Include(s => s.Module).FirstOrDefaultAsync(s => s.SlotId == slotId);
    }
}
