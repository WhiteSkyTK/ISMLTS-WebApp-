namespace ISMLTS_WebApp_.Models
{
    public class ModuleMarksViewModel
    {
        public int ModuleId { get; set; }
        public string ModuleDisplay { get; set; } = string.Empty;
        public List<StudentMarksRow> Rows { get; set; } = new();
    }
}
