namespace ISMLTS_WebApp_.Models
{
    public class NotificationBellViewModel
    {
        public int UnreadCount { get; set; }
        public List<Notification> Latest { get; set; } = new();
        public List<string> DueSoon { get; set; } = new();
    }
}
