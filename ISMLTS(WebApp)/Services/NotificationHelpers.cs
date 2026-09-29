using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Services
{
    public static class LocalUrl
    {
        // Same rule as Url.IsLocalUrl: "/path" is fine, "//host" and "/\host" would leave the site
        public static bool IsLocal(string? url) =>
            !string.IsNullOrEmpty(url) && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
    }

    public static class TimeAgo
    {
        // "Just now", "5 min ago", "3 hours ago", "Yesterday", "4 days ago", then the date
        public static string Describe(DateTime createdUtc, DateTime nowUtc)
        {
            var age = nowUtc - createdUtc;
            if (age < TimeSpan.FromMinutes(1)) return "Just now";
            if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes} min ago";
            if (age < TimeSpan.FromDays(1))
            {
                var hours = (int)age.TotalHours;
                return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
            }
            if (age < TimeSpan.FromDays(7))
            {
                var days = (int)age.TotalDays;
                return days == 1 ? "Yesterday" : $"{days} days ago";
            }
            return createdUtc.ToLocalTime().ToString("dd MMM yyyy");
        }
    }

    public static class AnnouncementAudience
    {
        // myModuleIds: the modules a student is enrolled in, or the modules a lecturer teaches
        public static bool CanSee(Announcement announcement, string role, IReadOnlyCollection<int> myModuleIds)
        {
            if (role == Roles.Admin) return true;
            if (announcement.ModuleId is int moduleId) return myModuleIds.Contains(moduleId);
            return announcement.Audience == AnnouncementAudiences.Everyone
                || (announcement.Audience == AnnouncementAudiences.Students && role == Roles.Student)
                || (announcement.Audience == AnnouncementAudiences.Lecturers && role == Roles.Lecturer);
        }

        // "XADAD7112", "Everyone", "All students" or "All lecturers"
        public static string Describe(Announcement announcement) =>
            announcement.Module?.Code ?? announcement.Audience switch
            {
                AnnouncementAudiences.Students => "All students",
                AnnouncementAudiences.Lecturers => "All lecturers",
                _ => "Everyone"
            };
    }
}
