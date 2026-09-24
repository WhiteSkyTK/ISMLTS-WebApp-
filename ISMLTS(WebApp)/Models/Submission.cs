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
                return Assessment != null && SubmittedAt > Assessment.DueDate ? "Late" : "Submitted";
            }
        }
    }

}