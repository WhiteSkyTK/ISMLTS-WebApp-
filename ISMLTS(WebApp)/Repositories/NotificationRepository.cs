using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public class NotificationRepository : Repository<Notification>, INotificationRepository
    {
        public NotificationRepository(ApplicationDbContext context) : base(context) { }

        public async Task AddRangeAsync(IEnumerable<Notification> notifications) => await _dbSet.AddRangeAsync(notifications);

        public async Task<int> CountUnreadAsync(string role, int userId) =>
            await _dbSet.CountAsync(n => n.Role == role && n.UserId == userId && !n.IsRead);

        public async Task<List<Notification>> GetLatestAsync(string role, int userId, int take) =>
            await ForUser(role, userId).Take(take).ToListAsync();

        public async Task<PagedList<Notification>> GetPageAsync(string role, int userId, bool unreadOnly, int page)
        {
            var notifications = ForUser(role, userId);
            if (unreadOnly)
            {
                notifications = notifications.Where(n => !n.IsRead);
            }
            return await notifications.ToPagedListAsync(page, null);
        }

        public async Task<Notification?> GetForUserAsync(string role, int userId, int notificationId) =>
            await _dbSet.FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.Role == role && n.UserId == userId);

        // null ids = mark everything read; the Role/UserId filter means nobody can mark someone else's
        public async Task<int> MarkReadAsync(string role, int userId, IReadOnlyCollection<int>? notificationIds)
        {
            var unread = _dbSet.Where(n => n.Role == role && n.UserId == userId && !n.IsRead);
            if (notificationIds != null)
            {
                unread = unread.Where(n => notificationIds.Contains(n.NotificationId));
            }
            return await unread.ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        }

        private IQueryable<Notification> ForUser(string role, int userId) =>
            _dbSet.AsNoTracking()
                .Where(n => n.Role == role && n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.NotificationId);
    }
}
