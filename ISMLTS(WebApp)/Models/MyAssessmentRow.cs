namespace ISMLTS_WebApp_.Models
{
    public class MyAssessmentRow
    {
        public int AssessmentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Not Submitted";
        public string? Link { get; set; }
    }
}
