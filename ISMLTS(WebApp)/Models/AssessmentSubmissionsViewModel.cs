namespace ISMLTS_WebApp_.Models
{
    public class AssessmentSubmissionsViewModel
    {
        public int AssessmentId { get; set; }
        public string AssessmentDisplay { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public List<SubmissionRow> Rows { get; set; } = new();
    }
}
