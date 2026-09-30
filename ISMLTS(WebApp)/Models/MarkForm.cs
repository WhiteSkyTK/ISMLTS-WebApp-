using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    // What the Add/Edit mark form posts. Checked by MarkRules in the controller, not by attributes,
    // because "out of" comes from the assessment when one is picked.
    public class MarkForm
    {
        public int? MarkId { get; set; }
        public int StudentId { get; set; }
        public int ModuleId { get; set; }

        [Display(Name = "Assessment")]
        public int? AssessmentId { get; set; }

        [MaxLength(100)]
        [Display(Name = "Name (only for work that isn't listed)")]
        public string? OtherName { get; set; }

        public decimal? Score { get; set; }

        [Display(Name = "Out of (only for work that isn't listed)")]
        public decimal? OutOf { get; set; } = 100;

        [Display(Name = "Feedback for the student")]
        public string? Feedback { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date captured")]
        public DateTime DateCaptured { get; set; } = DateTime.Today;
    }
}
