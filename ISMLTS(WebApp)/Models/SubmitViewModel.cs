namespace ISMLTS_WebApp_.Models
{
    // The student's hand-in page: the link they gave, every file they uploaded (newest first), and whether it's still open
    public class SubmitViewModel
    {
        public int AssessmentId { get; set; }
        public string AssessmentDisplay { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public int? LateDays { get; set; }
        public bool IsOpen { get; set; } = true;
        public string? Link { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public List<SubmissionFile> Files { get; set; } = new();
        public int MaxMegabytes { get; set; }
    }
}
