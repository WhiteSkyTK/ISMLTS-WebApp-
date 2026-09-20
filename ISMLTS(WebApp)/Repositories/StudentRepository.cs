using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Data;

namespace ISMLTS_WebApp_.Repositories
{
    public class StudentRepository : Repository<Student>, IStudentRepository
    {
        public StudentRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Student?> GetByEmailAsync(string email) =>
            await _dbSet.FirstOrDefaultAsync(s => s.Email == email);

        public async Task<IEnumerable<Student>> GetByModuleAsync(int moduleId) =>
            await _dbSet.Where(s => s.Modules.Any(m => m.ModuleId == moduleId)).ToListAsync();
    }
}