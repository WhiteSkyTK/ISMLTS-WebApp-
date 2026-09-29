using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ISMLTS_WebApp_.Models
{
    public class Submission
    {
        [Key]
        public int SubmissionId { get; set; }

        [ForeignKey(nameof(Assessment))]
        public int AssessmentId { get; set; }
        public Assessment? Assessment { get; set; }

        [ForeignKey(nameof(Student))]
        public int StudentId { get; set; }
        public Student? Student { get; set; }

        public DateTime? SubmittedAt { get; set; }

        [MaxLength(300)]
        public string? Link { get; set; }

        [NotMapped]
        public string Status
        {
            get
            {
                if (SubmittedAt == null) return "Not Submitted";
                // SubmittedAt is UTC; DueDate is a local calendar date, so anything on the due day counts as on time
                return Assessment != null && SubmittedAt.Value.ToLocalTime().Date > Assessment.DueDate.Date ? "Late" : "Submitted";
            }
        }
    }

}