namespace ISMLTS_WebApp_.Models
{
    public class SubmissionRow
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int? SubmissionId { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public string? Link { get; set; }
        public string Status { get; set; } = "Not Submitted";
    }
}
