using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ISMLTS_WebApp_.Models
{
    public static class AnnouncementAudiences
    {
        public const string Everyone = "Everyone";
        public const string Students = "Students";
        public const string Lecturers = "Lecturers";
    }

    // Lecturers post to one of their modules (ModuleId set); admins post college-wide to an audience (ModuleId null)
    public class Announcement
    {
        [Key]
        public int AnnouncementId { get; set; }

        [Required, MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(2000)]
        [Display(Name = "Message")]
        public string Body { get; set; } = string.Empty;

        [ForeignKey(nameof(Module))]
        [Display(Name = "Module")]
        public int? ModuleId { get; set; }
        public Module? Module { get; set; }

        [Required, MaxLength(20)]
        [Display(Name = "Who should see it")]
        public string Audience { get; set; } = AnnouncementAudiences.Students;

        [Required, MaxLength(20)]
        public string AuthorRole { get; set; } = string.Empty;

        public int AuthorId { get; set; }

        [Required, MaxLength(100)]
        public string AuthorName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
