namespace ISMLTS_WebApp_.Models
{
    public class AssessmentSubmissionsViewModel
    {
        public int AssessmentId { get; set; }
        public int ModuleId { get; set; }
        public string AssessmentDisplay { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public int? LateDays { get; set; }
        public List<SubmissionRow> Rows { get; set; } = new();
    }
}
