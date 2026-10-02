using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Data;

namespace ISMLTS_WebApp_.Repositories
{
    // For the admin Site check page: can the site reach its database, is the schema up to date, how much data is there
    public class DatabaseInfoRepository : IDatabaseInfoRepository
    {
        private readonly ApplicationDbContext _context;

        public DatabaseInfoRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DatabaseInfo> GetAsync()
        {
            if (!await _context.Database.CanConnectAsync())
                return new DatabaseInfo(false, Array.Empty<string>(), null, 0, 0, 0, 0);

            return new DatabaseInfo(
                true,
                (await _context.Database.GetPendingMigrationsAsync()).ToList(),
                (await _context.Database.GetAppliedMigrationsAsync()).LastOrDefault(),
                await _context.Students.CountAsync(),
                await _context.Lecturers.CountAsync(),
                await _context.Modules.CountAsync(),
                await _context.Courses.CountAsync());
        }
    }
}
