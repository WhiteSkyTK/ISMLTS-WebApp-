using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Data;

namespace ISMLTS_WebApp_.Repositories
{
    public class ModuleRepository : Repository<Module>, IModuleRepository
    {
        public ModuleRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Module?> GetByCodeAsync(string code) =>
            await _dbSet.FirstOrDefaultAsync(m => m.Code == code);

        public async Task<IEnumerable<Module>> GetByLecturerAsync(int lecturerId) =>
            await _dbSet.Where(m => m.LecturerId == lecturerId).ToListAsync();
    }
}