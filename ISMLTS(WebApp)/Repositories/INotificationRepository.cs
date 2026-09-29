using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface INotificationRepository : IRepository<Notification>
    {
        Task AddRangeAsync(IEnumerable<Notification> notifications);
        Task<int> CountUnreadAsync(string role, int userId);
        Task<List<Notification>> GetLatestAsync(string role, int userId, int take);
        Task<PagedList<Notification>> GetPageAsync(string role, int userId, bool unreadOnly, int page);
        Task<Notification?> GetForUserAsync(string role, int userId, int notificationId);
        Task<int> MarkReadAsync(string role, int userId, IReadOnlyCollection<int>? notificationIds);
    }
}
