using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Data;

namespace ISMLTS_WebApp_.Repositories
{
    public class CourseRepository : Repository<Course>, ICourseRepository
    {
        public CourseRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Course?> GetByIdWithModulesAsync(int id) =>
            await _dbSet.Include(c => c.Modules).FirstOrDefaultAsync(c => c.CourseId == id);
    }
}