namespace ISMLTS_WebApp_.Models
{
    public class MyModuleMarks
    {
        public string ModuleDisplay { get; set; } = string.Empty;
        public List<Mark> Marks { get; set; } = new();
        public decimal AveragePercentage { get; set; }
        public bool IsAtRisk { get; set; }
    }
}
