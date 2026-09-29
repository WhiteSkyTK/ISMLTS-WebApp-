using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface INotificationSettingRepository : IRepository<NotificationSetting>
    {
        Task<NotificationSetting?> GetAsync(string role, int userId);
        Task<Dictionary<int, NotificationSetting>> GetForRoleAsync(string role);
    }
}
