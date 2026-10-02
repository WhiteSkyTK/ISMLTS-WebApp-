using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Repositories
{
    public class StudentRepository : Repository<Student>, IStudentRepository
    {
        public StudentRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Student?> GetByEmailAsync(string email) =>
            await _dbSet.FirstOrDefaultAsync(s => s.Email == email);

        public async Task<Student?> GetByCalendarTokenHashAsync(string tokenHash) =>
            await _dbSet.AsNoTracking().FirstOrDefaultAsync(s => s.CalendarTokenHash == tokenHash);

        public async Task<IEnumerable<Student>> GetByModuleAsync(int moduleId) =>
            await _dbSet.Where(s => s.Modules.Any(m => m.ModuleId == moduleId)).ToListAsync();

        // With each module's lecturer and course, for the student pages and the app
        public async Task<Student?> GetByIdWithModulesAsync(int id) =>
            await _dbSet.Include(s => s.Modules).ThenInclude(m => m.Lecturer)
                .Include(s => s.Modules).ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(s => s.StudentId == id);

        public async Task<bool> IsEnrolledAsync(int studentId, int moduleId) =>
            await _dbSet.AnyAsync(s => s.StudentId == studentId && s.Modules.Any(m => m.ModuleId == moduleId));

        public async Task<bool> EmailExistsAsync(string email, int exceptStudentId = 0) =>
            await _dbSet.AnyAsync(s => s.Email == email && s.StudentId != exceptStudentId);

        public async Task<PagedList<Student>> SearchAsync(string? query, int page, string? sort = null, string? programme = null)
        {
            var students = _dbSet.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim();
                students = students.Where(s => s.FullName.Contains(term) || s.Email.Contains(term)
                    || (s.Programme != null && s.Programme.Contains(term)));
            }
            if (programme == ListFilters.None)
                students = students.Where(s => s.Programme == null || s.Programme == "");
            else if (!string.IsNullOrEmpty(programme))
                students = students.Where(s => s.Programme == programme);

            var order = ListSort.Parse(sort, "name", "name", "email", "programme");
            students = order.Key switch
            {
                "email" => students.OrderBy(s => s.Email, order.Descending),
                "programme" => students.OrderBy(s => s.Programme, order.Descending).ThenBy(s => s.FullName),
                _ => students.OrderBy(s => s.FullName, order.Descending)
            };
            return await students.ToPagedListAsync(page, query, order);
        }

        public async Task<List<string>> GetProgrammesAsync() =>
            await _dbSet.Where(s => s.Programme != null && s.Programme != "").Select(s => s.Programme!).Distinct().OrderBy(p => p).ToListAsync();

        public async Task<IEnumerable<Student>> GetByIdsAsync(IEnumerable<int> ids)
        {
            var wanted = ids.Distinct().ToList();
            return await _dbSet.Where(s => wanted.Contains(s.StudentId)).ToListAsync();
        }

        public async Task<List<Student>> GetAllWithModulesAsync() =>
            await _dbSet.AsNoTracking().Include(s => s.Modules).OrderBy(s => s.StudentId).ToListAsync();
    }
}