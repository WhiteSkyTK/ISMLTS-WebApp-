using System.Globalization;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Services
{
    // What one student has done in one module; marks are only the ones the student may see
    public record StudentModuleData(
        Module Module,
        IReadOnlyCollection<Mark> Marks,
        int SessionsHeld,
        int SessionsAttended,
        IReadOnlyCollection<Assessment> Assessments,
        IReadOnlySet<int> SubmittedAssessmentIds);

    public record ModuleProgress(
        int ModuleId,
        string Code,
        string Name,
        decimal? Average,
        int? AttendancePercent,
        int SessionsAttended,
        int SessionsHeld,
        int Submitted,
        int AssessmentCount,
        Assessment? NextDue,
        IReadOnlyList<string> RiskReasons)
    {
        public bool AtRisk => RiskReasons.Count > 0;
    }

    public record ProgressSummary(decimal? OverallAverage, int? AttendancePercent, int Submitted, int AssessmentCount, int ModulesAtRisk, int ModuleCount);

    public static class Progress
    {
        // Whole-number percentage, or null when there's nothing to measure yet
        public static int? Percent(int part, int whole) =>
            whole == 0 ? null : (int)Math.Round(part * 100m / whole, MidpointRounding.AwayFromZero);

        // At risk: average below 50% (once there are marks) or attendance below the threshold (once sessions were held)
        public static List<string> RiskReasons(decimal? average, int? attendancePercent, int attendanceThreshold)
        {
            var reasons = new List<string>();
            if (average < RiskCalculator.AtRiskThreshold)
                reasons.Add($"Average {average.Value.ToString("0.#", CultureInfo.InvariantCulture)}% is below {RiskCalculator.AtRiskThreshold.ToString("0", CultureInfo.InvariantCulture)}%");
            if (attendancePercent < attendanceThreshold)
                reasons.Add($"Attendance {attendancePercent}% is below {attendanceThreshold}%");
            return reasons;
        }

        public static ModuleProgress ForModule(StudentModuleData data, DateTime today, int attendanceThreshold)
        {
            decimal? average = data.Marks.Count > 0 ? RiskCalculator.AveragePercentage(data.Marks) : null;
            var attendance = Percent(data.SessionsAttended, data.SessionsHeld);
            var nextDue = data.Assessments
                .Where(a => a.DueDate.Date >= today.Date && !data.SubmittedAssessmentIds.Contains(a.AssessmentId))
                .OrderBy(a => a.DueDate)
                .FirstOrDefault();

            return new ModuleProgress(
                data.Module.ModuleId,
                data.Module.Code,
                data.Module.Name,
                average,
                attendance,
                data.SessionsAttended,
                data.SessionsHeld,
                data.Assessments.Count(a => data.SubmittedAssessmentIds.Contains(a.AssessmentId)),
                data.Assessments.Count,
                nextDue,
                RiskReasons(average, attendance, attendanceThreshold));
        }

        public static ProgressSummary Summarise(IReadOnlyCollection<ModuleProgress> modules)
        {
            var averages = modules.Where(m => m.Average != null).Select(m => m.Average!.Value).ToList();
            return new ProgressSummary(
                averages.Count > 0 ? Math.Round(averages.Average(), 1) : null,
                Percent(modules.Sum(m => m.SessionsAttended), modules.Sum(m => m.SessionsHeld)),
                modules.Sum(m => m.Submitted),
                modules.Sum(m => m.AssessmentCount),
                modules.Count(m => m.AtRisk),
                modules.Count);
        }
    }
}
