using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Data;

namespace ISMLTS_WebApp_.Repositories
{
    public class MarkRepository : Repository<Mark>, IMarkRepository
    {
        public MarkRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Mark>> GetByModuleAsync(int moduleId) =>
            await _dbSet.Where(m => m.ModuleId == moduleId).ToListAsync();

        public async Task<IEnumerable<Mark>> GetByStudentAsync(int studentId) =>
            await _dbSet.Include(m => m.Module).Where(m => m.StudentId == studentId).ToListAsync();

        public async Task<Mark?> GetByIdWithDetailsAsync(int id) =>
            await _dbSet.Include(m => m.Student).Include(m => m.Module).FirstOrDefaultAsync(m => m.MarkId == id);
    }
}