using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public class NotificationSettingRepository : Repository<NotificationSetting>, INotificationSettingRepository
    {
        public NotificationSettingRepository(ApplicationDbContext context) : base(context) { }

        public async Task<NotificationSetting?> GetAsync(string role, int userId) =>
            await _dbSet.FirstOrDefaultAsync(s => s.Role == role && s.UserId == userId);

        public async Task<Dictionary<int, NotificationSetting>> GetForRoleAsync(string role) =>
            await _dbSet.Where(s => s.Role == role).ToDictionaryAsync(s => s.UserId);
    }
}
