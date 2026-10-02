namespace ISMLTS_WebApp_.Models
{
    public class TimetablePageModel
    {
        public List<Module> Modules { get; set; } = new();
        public List<TimetableSlot> Slots { get; set; } = new();
        public TimetableSlot Form { get; set; } = new();
    }
}
