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
    }
}