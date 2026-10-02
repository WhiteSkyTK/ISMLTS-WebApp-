using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Models
{
    public class CalendarPageModel
    {
        public DateTime Month { get; set; }
        public DateTime GridStart { get; set; }
        public DateTime GridEnd { get; set; }
        public List<CalendarEntry> Entries { get; set; } = new();
        public CalendarNote NewNote { get; set; } = new();

        // Only on the response that created it
        public string? FeedUrl { get; set; }
    }
}
