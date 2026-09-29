using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public class MarkChangeRepository : Repository<MarkChange>, IMarkChangeRepository
    {
        public MarkChangeRepository(ApplicationDbContext context) : base(context) { }

        public async Task<List<MarkChange>> GetByMarkAsync(int markId) =>
            await _dbSet.AsNoTracking().Where(c => c.MarkId == markId)
                .OrderByDescending(c => c.ChangedAt).ThenByDescending(c => c.MarkChangeId)
                .ToListAsync();
    }
}
