using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    public static class CollegeDateKinds
    {
        public const string Holiday = "Holiday";
        public const string Exams = "Exams";
        public const string Assignments = "Assignments";
        public const string Break = "Break";
        public const string Deadline = "Deadline";
        public const string Other = "Other";

        public static readonly IReadOnlyList<(string Kind, string Label)> All =
        [
            (Holiday, "Public holiday"), (Exams, "Exam weeks"), (Assignments, "Assignment weeks"),
            (Break, "Break"), (Deadline, "Closing date"), (Other, "Other")
        ];

        public static bool IsValid(string? kind) => All.Any(k => k.Kind == kind);
    }

    // A college-wide date on everyone's calendar: holidays, exam and assignment weeks, breaks and closing dates
    public class CollegeDate
    {
        [Key]
        public int CollegeDateId { get; set; }

        [Required, MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Kind { get; set; } = CollegeDateKinds.Holiday;

        [DataType(DataType.Date)]
        [Display(Name = "From")]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "To")]
        public DateTime EndDate { get; set; }
    }
}
