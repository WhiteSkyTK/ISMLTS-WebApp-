namespace ISMLTS_WebApp_.Models
{
    public class MyAssessmentRow
    {
        public int AssessmentId { get; set; }
        public int ModuleId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ModuleCode { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime DueDate { get; set; }
        public decimal MaxScore { get; set; } = 100;
        public string Status { get; set; } = "Not Submitted";
        public DateTime? SubmittedAt { get; set; }
        public string? Link { get; set; }

        // The latest uploaded file (it's the one that counts) and how many were uploaded in all
        public SubmissionFile? File { get; set; }
        public int FileCount { get; set; }

        // Null keeps accepting late work; otherwise the last day work is accepted
        public DateTime? LastDay { get; set; }
        public bool SubmissionsOpen { get; set; } = true;

        // Only set once the lecturer releases the assessment's marks
        public Mark? Mark { get; set; }
        public bool MarksReleased { get; set; }
    }
}
