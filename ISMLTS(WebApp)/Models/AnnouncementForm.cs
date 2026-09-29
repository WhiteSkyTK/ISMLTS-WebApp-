using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    // What the Create form posts; author details are filled in from the signed-in user, never from the form
    public class AnnouncementForm
    {
        [Required, MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(2000)]
        [Display(Name = "Message")]
        public string Body { get; set; } = string.Empty;

        [Display(Name = "Module")]
        public int? ModuleId { get; set; }

        [Display(Name = "Who should see it")]
        public string Audience { get; set; } = AnnouncementAudiences.Everyone;
    }
}
