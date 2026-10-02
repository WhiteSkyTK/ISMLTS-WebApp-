using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public class CollegeDateRepository : Repository<CollegeDate>, ICollegeDateRepository
    {
        public CollegeDateRepository(ApplicationDbContext context) : base(context) { }

        public async Task<List<CollegeDate>> GetOrderedAsync() =>
            await _dbSet.AsNoTracking().OrderBy(d => d.StartDate).ToListAsync();

        public async Task<List<CollegeDate>> GetOverlappingAsync(DateTime from, DateTime to) =>
            await _dbSet.AsNoTracking().Where(d => d.StartDate < to && d.EndDate >= from).OrderBy(d => d.StartDate).ToListAsync();
    }
}
