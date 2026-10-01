namespace ISMLTS_WebApp_.Models
{
    public class CourseModuleRow
    {
        public int ModuleId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Term { get; set; } = "Term1";
        public bool IsInCourse { get; set; }
        public string? OtherCourseCode { get; set; }
    }

    public class CourseModulesViewModel
    {
        public int CourseId { get; set; }
        public string CourseDisplay { get; set; } = string.Empty;
        public List<CourseModuleRow> Modules { get; set; } = new();
    }

    public class CourseStudentRow
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Programme { get; set; }
        public int EnrolledModules { get; set; }
    }

    public class CourseEnrolmentViewModel
    {
        public int CourseId { get; set; }
        public string CourseDisplay { get; set; } = string.Empty;
        public int Term1Modules { get; set; }
        public int Term2Modules { get; set; }
        public int TotalModules => Term1Modules + Term2Modules;
        public List<CourseStudentRow> Students { get; set; } = new();
    }
}
