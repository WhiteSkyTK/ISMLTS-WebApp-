using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ISMLTS_WebApp_.Models
{
    public class Ticket
    {
        [Key]
        public int TicketId { get; set; }

        [ForeignKey(nameof(Student))]
        public int StudentId { get; set; }
        public Student? Student { get; set; }

        [ForeignKey(nameof(Module))]
        public int ModuleId { get; set; }
        public Module? Module { get; set; }

        [Required, MaxLength(150)]
        public string Subject { get; set; } = string.Empty;

        [Required, MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Open"; // Open, In Progress, Resolved

        [MaxLength(1000)]
        public string? LecturerResponse { get; set; }

        public DateTime DateOpened { get; set; } = DateTime.Now;
        public DateTime? DateResolved { get; set; }
    }
}