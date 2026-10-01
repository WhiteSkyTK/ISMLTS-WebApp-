namespace ISMLTS_WebApp_.Models
{
    public class EnrolmentRow
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Programme { get; set; }
        public bool IsEnrolled { get; set; }
    }
}
