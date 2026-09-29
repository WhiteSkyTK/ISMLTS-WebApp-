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
    }
}