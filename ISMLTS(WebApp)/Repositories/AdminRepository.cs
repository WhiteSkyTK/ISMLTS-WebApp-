using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Data;

namespace ISMLTS_WebApp_.Repositories
{
    public class AdminRepository : Repository<Admin>, IAdminRepository
    {
        public AdminRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Admin?> GetByUsernameAsync(string username) =>
            await _dbSet.FirstOrDefaultAsync(a => a.Username == username);

        public async Task<bool> UsernameExistsAsync(string username, int exceptAdminId = 0) =>
            await _dbSet.AnyAsync(a => a.Username == username && a.AdminId != exceptAdminId);

        public async Task<PagedList<Admin>> SearchAsync(string? query, int page)
        {
            var admins = _dbSet.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim();
                admins = admins.Where(a => a.Username.Contains(term));
            }
            return await admins.OrderBy(a => a.Username).ToPagedListAsync(page, query);
        }
    }
}