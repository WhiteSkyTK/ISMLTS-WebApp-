using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public class ApiRefreshTokenRepository : Repository<ApiRefreshToken>, IApiRefreshTokenRepository
    {
        public ApiRefreshTokenRepository(ApplicationDbContext context) : base(context) { }

        public async Task<ApiRefreshToken?> GetByHashAsync(string tokenHash) =>
            await _dbSet.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        public async Task RevokeAllAsync(string role, int userId, DateTime now) =>
            await _dbSet.Where(t => t.Role == role && t.UserId == userId && t.RevokedAt == null)
                .ExecuteUpdateAsync(t => t.SetProperty(x => x.RevokedAt, now));
    }
}
