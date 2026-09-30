using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Services
{
    // Everything about one module a lecturer needs; Module.Students must be loaded. Lecturers see unreleased marks too.
    public record ClassModuleData(
        Module Module,
        IReadOnlyCollection<Mark> Marks,
        int SessionsHeld,
        IReadOnlyCollection<AttendanceRecord> Records,
        IReadOnlyCollection<Assessment> Assessments,
        IReadOnlyCollection<Submission> Submissions);

    public record StudentInsight(
        int StudentId,
        string FullName,
        decimal? Average,
        int? AttendancePercent,
        int Submitted,
        int DueSoFar,
        IReadOnlyList<string> RiskReasons);

    public record ModuleInsight(
        int ModuleId,
        string Code,
        string Name,
        int Enrolled,
        decimal? ClassAverage,
        int? AttendanceRate,
        int? SubmissionRate,
        IReadOnlyList<StudentInsight> AtRisk);

    public static class ClassInsights
    {
        public static ModuleInsight ForModule(ClassModuleData data, DateTime today, int attendanceThreshold)
        {
            var students = data.Module.Students.ToList();
            var dueSoFar = data.Assessments.Where(a => a.DueDate.Date <= today.Date).Select(a => a.AssessmentId).ToHashSet();
            var submittedDue = data.Submissions
                .Where(s => s.SubmittedAt != null && dueSoFar.Contains(s.AssessmentId))
                .GroupBy(s => s.StudentId)
                .ToDictionary(g => g.Key, g => g.Select(s => s.AssessmentId).Distinct().Count());
            var attended = data.Records
                .GroupBy(r => r.StudentId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.SessionId).Distinct().Count());

            var insights = students.Select(s =>
            {
                var marks = data.Marks.Where(m => m.StudentId == s.StudentId).ToList();
                decimal? average = marks.Count > 0 ? RiskCalculator.AveragePercentage(marks) : null;
                var attendance = Progress.Percent(attended.GetValueOrDefault(s.StudentId), data.SessionsHeld);
                return new StudentInsight(
                    s.StudentId,
                    s.FullName,
                    average,
                    attendance,
                    submittedDue.GetValueOrDefault(s.StudentId),
                    dueSoFar.Count,
                    Progress.RiskReasons(average, attendance, attendanceThreshold));
            }).ToList();

            var averages = insights.Where(i => i.Average != null).Select(i => i.Average!.Value).ToList();
            var enrolledIds = students.Select(s => s.StudentId).ToHashSet();

            return new ModuleInsight(
                data.Module.ModuleId,
                data.Module.Code,
                data.Module.Name,
                students.Count,
                averages.Count > 0 ? Math.Round(averages.Average(), 1) : null,
                Progress.Percent(attended.Where(a => enrolledIds.Contains(a.Key)).Sum(a => a.Value), data.SessionsHeld * students.Count),
                Progress.Percent(submittedDue.Where(s => enrolledIds.Contains(s.Key)).Sum(s => s.Value), dueSoFar.Count * students.Count),
                insights.Where(i => i.RiskReasons.Count > 0)
                    .OrderByDescending(i => i.RiskReasons.Count)
                    .ThenBy(i => i.Average ?? 100)
                    .ThenBy(i => i.FullName)
                    .ToList());
        }
    }
}
