using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;
using ISMLTS_WebApp_.Data;

namespace ISMLTS_WebApp_.Repositories
{
    public class CourseRepository : Repository<Course>, ICourseRepository
    {
        public CourseRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Course?> GetByIdWithModulesAsync(int id) =>
            await _dbSet.Include(c => c.Modules).FirstOrDefaultAsync(c => c.CourseId == id);

        public async Task<bool> CodeExistsAsync(string code, int exceptCourseId = 0) =>
            await _dbSet.AnyAsync(c => c.Code == code && c.CourseId != exceptCourseId);

        public async Task<PagedList<Course>> SearchAsync(string? query, int page, string? sort = null)
        {
            var courses = _dbSet.AsNoTracking().Include(c => c.Modules).AsQueryable();
            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim();
                courses = courses.Where(c => c.Code.Contains(term) || c.Name.Contains(term));
            }
            var order = ListSort.Parse(sort, "code", "code", "name", "modules");
            courses = order.Key switch
            {
                "name" => courses.OrderBy(c => c.Name, order.Descending),
                "modules" => courses.OrderBy(c => c.Modules.Count, order.Descending).ThenBy(c => c.Code),
                _ => courses.OrderBy(c => c.Code, order.Descending)
            };
            return await courses.ToPagedListAsync(page, query, order);
        }
    }
}