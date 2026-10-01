using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;
using ISMLTS_WebApp_.Data;

namespace ISMLTS_WebApp_.Repositories
{
    public class ModuleRepository : Repository<Module>, IModuleRepository
    {
        public ModuleRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Module?> GetByCodeAsync(string code) =>
            await _dbSet.FirstOrDefaultAsync(m => m.Code == code);

        public async Task<bool> CodeExistsAsync(string code, int exceptModuleId = 0) =>
            await _dbSet.AnyAsync(m => m.Code == code && m.ModuleId != exceptModuleId);

        public async Task<IEnumerable<Module>> GetByLecturerAsync(int lecturerId) =>
            await _dbSet.Where(m => m.LecturerId == lecturerId).ToListAsync();

        public async Task<PagedList<Module>> SearchAsync(string? query, int page, string? sort = null, ModuleListFilter? filter = null)
        {
            var modules = _dbSet.AsNoTracking().Include(m => m.Lecturer).Include(m => m.Course).AsQueryable();
            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim();
                modules = modules.Where(m => m.Code.Contains(term) || m.Name.Contains(term)
                    || (m.Lecturer != null && m.Lecturer.FullName.Contains(term)));
            }
            if (filter?.CourseId is int courseId)
                modules = courseId == 0 ? modules.Where(m => m.CourseId == null) : modules.Where(m => m.CourseId == courseId);
            if (!string.IsNullOrEmpty(filter?.Term))
                modules = modules.Where(m => m.Term == filter.Term);
            if (filter?.LecturerId is int lecturerId)
                modules = modules.Where(m => m.LecturerId == lecturerId);

            var order = ListSort.Parse(sort, "code", "code", "name", "lecturer", "term");
            modules = order.Key switch
            {
                "name" => modules.OrderBy(m => m.Name, order.Descending),
                "lecturer" => modules.OrderBy(m => m.Lecturer!.FullName, order.Descending).ThenBy(m => m.Code),
                "term" => modules.OrderBy(m => m.Term, order.Descending).ThenBy(m => m.Code),
                _ => modules.OrderBy(m => m.Code, order.Descending)
            };
            return await modules.ToPagedListAsync(page, query, order);
        }

        public async Task<Module?> GetByIdWithDetailsAsync(int id) =>
            await _dbSet.Include(m => m.Lecturer).Include(m => m.Course).Include(m => m.Students)
                .FirstOrDefaultAsync(m => m.ModuleId == id);

        // Tracked, so the course pages can move modules between courses
        public async Task<List<Module>> GetAllWithCourseAsync() =>
            await _dbSet.Include(m => m.Course).OrderBy(m => m.Term).ThenBy(m => m.Code).ToListAsync();

        public async Task<List<Module>> GetByCourseWithStudentsAsync(int courseId) =>
            await _dbSet.Include(m => m.Students).Where(m => m.CourseId == courseId)
                .OrderBy(m => m.Term).ThenBy(m => m.Code).ToListAsync();
    }
}