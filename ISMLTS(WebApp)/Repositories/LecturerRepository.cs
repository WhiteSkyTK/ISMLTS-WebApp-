using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Data;

namespace ISMLTS_WebApp_.Repositories
{
    public class LecturerRepository : Repository<Lecturer>, ILecturerRepository
    {
        public LecturerRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Lecturer?> GetByEmailAsync(string email) =>
            await _dbSet.FirstOrDefaultAsync(l => l.Email == email);

        public async Task<bool> EmailExistsAsync(string email, int exceptLecturerId = 0) =>
            await _dbSet.AnyAsync(l => l.Email == email && l.LecturerId != exceptLecturerId);

        public async Task<PagedList<Lecturer>> SearchAsync(string? query, int page)
        {
            var lecturers = _dbSet.AsNoTracking().Include(l => l.Modules).AsQueryable();
            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim();
                lecturers = lecturers.Where(l => l.FullName.Contains(term) || l.Email.Contains(term));
            }
            return await lecturers.OrderBy(l => l.FullName).ToPagedListAsync(page, query);
        }
    }
}