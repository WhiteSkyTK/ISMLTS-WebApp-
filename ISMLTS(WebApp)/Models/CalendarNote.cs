using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    // A person's own note or to-do on their calendar; with Remind set, the bell reminds them at its time
    public class CalendarNote
    {
        [Key]
        public int NoteId { get; set; }

        [Required, MaxLength(10)]
        public string Role { get; set; } = string.Empty;

        public int UserId { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        public TimeOnly? Time { get; set; }

        [Required, MaxLength(120)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Details { get; set; }

        public bool IsDone { get; set; }

        [Display(Name = "Remind me")]
        public bool Remind { get; set; }

        public DateTime? RemindedAt { get; set; }
    }
}
