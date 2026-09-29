using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    // Email choices per user; a missing row means the defaults (both on)
    public class NotificationSetting
    {
        [Key]
        public int NotificationSettingId { get; set; }

        [Required, MaxLength(20)]
        public string Role { get; set; } = string.Empty;

        public int UserId { get; set; }

        [Display(Name = "Email me about ticket replies")]
        public bool EmailEnabled { get; set; } = true;

        [Display(Name = "Send me a \"due this week\" email on Monday mornings")]
        public bool WeeklyDigest { get; set; } = true;

        public DateTime? LastDigestSentAt { get; set; }
    }
}
