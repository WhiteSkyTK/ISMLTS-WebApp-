namespace ISMLTS_WebApp_.Models
{
    public class UpcomingTask
    {
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // ICE, Quiz, POE, Project
        public DateTime DueDate { get; set; }
    }

    public class CourseCard
    {
        public int ModuleId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsCurrentSemester { get; set; }
        public double Rating { get; set; }
        public int DiscussionCount { get; set; }
    }

    public class DashboardViewModel
    {
        public int TotalStudents { get; set; }
        public int TotalLecturers { get; set; }
        public int TotalModules { get; set; }
        public List<string> Announcements { get; set; } = new();
        public List<UpcomingTask> UpcomingTasks { get; set; } = new();
        public List<CourseCard> Courses { get; set; } = new();
    }
}