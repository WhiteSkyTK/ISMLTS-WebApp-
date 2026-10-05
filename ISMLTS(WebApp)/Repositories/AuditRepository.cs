using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public class AuditRepository : Repository<AuditEntry>, IAuditRepository
    {
        public AuditRepository(ApplicationDbContext context) : base(context) { }

        public async Task<PagedList<AuditEntry>> SearchAsync(string? query, int page, string? action)
        {
            var entries = _dbSet.AsNoTracking();
            query = query?.Trim();
            if (!string.IsNullOrEmpty(query))
                entries = entries.Where(e => e.ActorName.Contains(query) || e.Target.Contains(query) || (e.Details != null && e.Details.Contains(query)));
            if (!string.IsNullOrEmpty(action))
                entries = entries.Where(e => e.Action == action);
            return await entries.OrderByDescending(e => e.At).ThenByDescending(e => e.AuditEntryId).ToPagedListAsync(page, query);
        }
    }
}
