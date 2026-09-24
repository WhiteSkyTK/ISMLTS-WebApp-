using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Data;

namespace ISMLTS_WebApp_.Repositories
{
    public class AssessmentRepository : Repository<Assessment>, IAssessmentRepository
    {
        public AssessmentRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Assessment>> GetByModuleAsync(int moduleId) =>
            await _dbSet.Where(a => a.ModuleId == moduleId).OrderBy(a => a.DueDate).ToListAsync();

        public async Task<Assessment?> GetByIdWithModuleAsync(int id) =>
            await _dbSet.Include(a => a.Module).FirstOrDefaultAsync(a => a.AssessmentId == id);

        public async Task<IEnumerable<Assessment>> GetUpcomingAsync(int take) =>
            await _dbSet.Include(a => a.Module)
                .Where(a => a.DueDate >= DateTime.Today)
                .OrderBy(a => a.DueDate)
                .Take(take)
                .ToListAsync();
    }
}