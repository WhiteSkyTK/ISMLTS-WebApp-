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

        // Only set once the lecturer releases the assessment's marks
        public Mark? Mark { get; set; }
        public bool MarksReleased { get; set; }
    }
}
