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
        public async Task<Student?> GetByIdWithModulesAsync(int id) =>
    await _dbSet.Include(s => s.Modules).FirstOrDefaultAsync(s => s.StudentId == id);

        public async Task<bool> IsEnrolledAsync(int studentId, int moduleId) =>
            await _dbSet.AnyAsync(s => s.StudentId == studentId && s.Modules.Any(m => m.ModuleId == moduleId));

        public async Task<bool> EmailExistsAsync(string email, int exceptStudentId = 0) =>
            await _dbSet.AnyAsync(s => s.Email == email && s.StudentId != exceptStudentId);

        public async Task<PagedList<Student>> SearchAsync(string? query, int page)
        {
            var students = _dbSet.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim();
                students = students.Where(s => s.FullName.Contains(term) || s.Email.Contains(term)
                    || (s.Programme != null && s.Programme.Contains(term)));
            }
            return await students.OrderBy(s => s.FullName).ToPagedListAsync(page, query);
        }

        public async Task<IEnumerable<Student>> GetByIdsAsync(IEnumerable<int> ids)
        {
            var wanted = ids.Distinct().ToList();
            return await _dbSet.Where(s => wanted.Contains(s.StudentId)).ToListAsync();
        }

        public async Task<List<Student>> GetAllWithModulesAsync() =>
            await _dbSet.AsNoTracking().Include(s => s.Modules).OrderBy(s => s.StudentId).ToListAsync();
    }
}