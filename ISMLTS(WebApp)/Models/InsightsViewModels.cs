using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Models
{
    public class MyProgressViewModel
    {
        public ProgressSummary Summary { get; set; } = new(null, null, 0, 0, 0, 0);
        public List<ModuleProgress> Modules { get; set; } = new();
        public int AttendanceThreshold { get; set; }
    }

    public class ClassInsightsViewModel
    {
        public List<ModuleInsight> Modules { get; set; } = new();
        public int AttendanceThreshold { get; set; }
    }
}
