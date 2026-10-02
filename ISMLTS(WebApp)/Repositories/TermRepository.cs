using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public class TermRepository : Repository<Term>, ITermRepository
    {
        public TermRepository(ApplicationDbContext context) : base(context) { }

        public async Task<List<Term>> GetOrderedAsync() =>
            await _dbSet.AsNoTracking().OrderByDescending(t => t.StartDate).ToListAsync();

        public async Task<bool> NameExistsAsync(string name, int exceptTermId = 0) =>
            await _dbSet.AnyAsync(t => t.Name == name && t.TermId != exceptTermId);
    }
}
