using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ISMLTS_WebApp_.Models
{
    public class AttendanceSession
    {
        [Key]
        public int SessionId { get; set; }

        [ForeignKey(nameof(Module))]
        public int ModuleId { get; set; }
        public Module? Module { get; set; }

        [Required, MaxLength(6)]
        public string Code { get; set; } = string.Empty;

        public DateTime StartedAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        // Lecturer's position when the session started; null if they blocked location access
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public bool IsClosed { get; set; }

        public ICollection<AttendanceRecord> Records { get; set; } = new List<AttendanceRecord>();

        [NotMapped]
        public bool IsOpen => !IsClosed && DateTime.UtcNow < ExpiresAt;
    }
}