using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Models
{
    public class CalendarPageModel
    {
        public List<CalendarEntry> Entries { get; set; } = new();
        public DateTime From { get; set; }

        // Only on the response that created it
        public string? FeedUrl { get; set; }
    }
}
