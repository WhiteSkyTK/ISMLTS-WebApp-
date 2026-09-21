namespace ISMLTS_WebApp_.Models
{
    public class EnrolmentViewModel
    {
        public int ModuleId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public List<EnrolmentRow> Students { get; set; } = new();
    }
}
