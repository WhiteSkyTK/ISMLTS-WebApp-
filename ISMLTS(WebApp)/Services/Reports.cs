using System.Globalization;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Services
{
    public record UserSummary(int Students, int StudentsNotEnrolled, int Lecturers, int LecturersWithoutModules, int Admins, int TwoFactorOn, int TotalUsers);

    // One module's line on the admin report: the same numbers lecturers see on Class Insights, plus who and where
    public record ModuleReport(ModuleInsight Insight, string Course, string Term, string Lecturer, int SessionsHeld);

    public static class Reports
    {
        public static UserSummary Users(IReadOnlyCollection<Student> students, IReadOnlyCollection<Lecturer> lecturers,
            IReadOnlyCollection<Admin> admins, IReadOnlyCollection<Module> modules)
        {
            var teaching = modules.Select(m => m.LecturerId).ToHashSet();
            var twoFactor = students.Count(s => s.TwoFactorEnabled) + lecturers.Count(l => l.TwoFactorEnabled) + admins.Count(a => a.TwoFactorEnabled);
            return new UserSummary(
                students.Count,
                students.Count(s => s.Modules.Count == 0),
                lecturers.Count,
                lecturers.Count(l => !teaching.Contains(l.LecturerId)),
                admins.Count,
                twoFactor,
                students.Count + lecturers.Count + admins.Count);
        }

        public static string ModulesCsv(IEnumerable<ModuleReport> modules)
        {
            static string Number(decimal? value) => value?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty;
            var header = new[] { "Module", "Name", "Course", "Term", "Lecturer", "Enrolled", "Class average %", "Attendance %", "Handed in %", "At risk", "Classes held" };
            return Csv.Write(new[] { header }.Concat(modules.Select(m => new[]
            {
                m.Insight.Code,
                m.Insight.Name,
                m.Course,
                m.Term,
                m.Lecturer,
                m.Insight.Enrolled.ToString(CultureInfo.InvariantCulture),
                Number(m.Insight.ClassAverage),
                Number(m.Insight.AttendanceRate),
                Number(m.Insight.SubmissionRate),
                m.Insight.AtRisk.Count.ToString(CultureInfo.InvariantCulture),
                m.SessionsHeld.ToString(CultureInfo.InvariantCulture)
            })));
        }
    }
}
