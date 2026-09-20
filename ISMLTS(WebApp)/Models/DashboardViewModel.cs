namespace ISMLTS_WebApp_.Models
{
    public class DashboardViewModel
    {
        public int TotalStudents { get; set; }
        public int TotalLecturers { get; set; }
        public int TotalModules { get; set; }
        public List<string> Announcements { get; set; } = new();
        public List<string> UpcomingDueDates { get; set; } = new();
    }
}