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

        // Every file the student uploaded; earlier ones are kept, the latest counts
        public List<SubmissionFile> Files { get; set; } = new();

        [NotMapped]
        public SubmissionFile? LatestFile => Files.OrderByDescending(f => f.UploadedAt).ThenByDescending(f => f.SubmissionFileId).FirstOrDefault();

        [NotMapped]
        public string Status => StatusFor(SubmittedAt, Assessment?.DueDate);

        public static string StatusFor(DateTime? submittedAt, DateTime? dueDate)
        {
            if (submittedAt == null) return "Not Submitted";
            // SubmittedAt is UTC; DueDate is a local calendar date, so anything on the due day counts as on time
            return dueDate != null && submittedAt.Value.ToLocalTime().Date > dueDate.Value.Date ? "Late" : "Submitted";
        }
    }

}