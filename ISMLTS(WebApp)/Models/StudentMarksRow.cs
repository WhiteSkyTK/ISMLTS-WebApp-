namespace ISMLTS_WebApp_.Models
{
    public class StudentMarksRow
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public List<Mark> Marks { get; set; } = new();
        public decimal AveragePercentage { get; set; }
        public bool IsAtRisk { get; set; }
    }
}
