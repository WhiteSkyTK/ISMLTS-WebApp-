using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    // One message for one user. Users live in three tables, so the recipient is Role + UserId rather than a foreign key.
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        [Required, MaxLength(20)]
        public string Role { get; set; } = string.Empty;

        public int UserId { get; set; }

        [Required, MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Message { get; set; }

        // Always a local path, e.g. /Marks/MyMarks
        [MaxLength(300)]
        public string? Url { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; }
    }
}
