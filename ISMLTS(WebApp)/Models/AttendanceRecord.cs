using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ISMLTS_WebApp_.Models
{
    public class AttendanceRecord
    {
        [Key]
        public int RecordId { get; set; }

        [ForeignKey(nameof(Session))]
        public int SessionId { get; set; }
        public AttendanceSession? Session { get; set; }

        [ForeignKey(nameof(Student))]
        public int StudentId { get; set; }
        public Student? Student { get; set; }

        public DateTime ScannedAt { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }
        public bool IpOnCampus { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? AccuracyMeters { get; set; }
        public double? DistanceMeters { get; set; }
        public bool LocationVerified { get; set; }

        public bool IsManual { get; set; }
    }

    public class AttendanceLiveViewModel
    {
        public AttendanceSession Session { get; set; } = new();
        public string ScanUrl { get; set; } = string.Empty;
        public string QrDataUri { get; set; } = string.Empty;
        public int SecondsLeft { get; set; }
    }

    public class AttendanceDetailsViewModel
    {
        public AttendanceSession Session { get; set; } = new();
        public List<Student> Absent { get; set; } = new();
    }

    public class MyAttendanceRow
    {
        public string ModuleDisplay { get; set; } = string.Empty;
        public int Attended { get; set; }
        public int Total { get; set; }
        public int Percentage => Total == 0 ? 0 : Attended * 100 / Total;
    }
}