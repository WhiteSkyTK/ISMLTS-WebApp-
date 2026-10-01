using System.Globalization;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Services
{
    public record AwardKind(string Key, string Title, string HowToEarn, string Icon);

    public record Award(string Kind, string Title, string Detail);

    // What awards are worked out from, all for one student. ClassMarks holds every student's marks in those
    // modules, so "top mark" can compare; only released marks count.
    public record StudentAwardData(
        int StudentId,
        IReadOnlyList<Module> Modules,
        IReadOnlyList<Assessment> Assessments,
        IReadOnlyCollection<Submission> Submissions,
        IReadOnlyCollection<Mark> ClassMarks,
        IReadOnlyDictionary<int, int> SessionsByModule,
        IReadOnlyDictionary<int, int> AttendedByModule);

    // Badges computed from records the site already keeps; nothing is stored
    public static class Awards
    {
        public const int AttendancePercent = 95;
        public const int MinimumSessions = 3;
        public const decimal DistinctionPercent = 75m;
        public const int MinimumMarks = 2;

        public const string Attendance = "attendance";
        public const string TopMark = "top-mark";
        public const string OnTime = "on-time";
        public const string Distinction = "distinction";

        public static readonly IReadOnlyList<AwardKind> Kinds = new[]
        {
            new AwardKind(Attendance, "Always there", $"Attend {AttendancePercent}% or more of a module's classes (once it has had at least {MinimumSessions}).", "bi-calendar-check"),
            new AwardKind(TopMark, "Top of the class", "Get the highest mark in an assessment once its marks are released.", "bi-trophy"),
            new AwardKind(OnTime, "Never late", "Hand in every assessment due so far in a term on time.", "bi-alarm"),
            new AwardKind(Distinction, "Distinction", $"Average {DistinctionPercent:0}% or more in a module over at least {MinimumMarks} released marks.", "bi-star")
        };

        public static List<Award> For(StudentAwardData data, DateTime today)
        {
            var awards = new List<Award>();
            awards.AddRange(AttendanceAwards(data));
            awards.AddRange(TopMarkAwards(data));
            awards.AddRange(OnTimeAwards(data, today));
            awards.AddRange(DistinctionAwards(data));
            return awards;
        }

        private static IEnumerable<Award> AttendanceAwards(StudentAwardData data) =>
            from module in data.Modules.OrderBy(m => m.Code)
            let sessions = data.SessionsByModule.GetValueOrDefault(module.ModuleId)
            let attended = data.AttendedByModule.GetValueOrDefault(module.ModuleId)
            where sessions >= MinimumSessions && attended * 100 >= AttendancePercent * sessions
            select new Award(Attendance, $"{AttendancePercent}%+ attendance in {module.Code}", $"Attended {attended} of {sessions} classes");

        private static IEnumerable<Award> TopMarkAwards(StudentAwardData data)
        {
            var released = data.ClassMarks.Where(m => m.AssessmentId != null && m.IsVisibleToStudent).ToLookup(m => m.AssessmentId!.Value);
            foreach (var assessment in data.Assessments.OrderBy(a => a.DueDate))
            {
                var marks = released[assessment.AssessmentId].ToList();
                var mine = marks.Find(m => m.StudentId == data.StudentId);
                if (marks.Count < MinimumMarks || mine == null) continue;

                var best = marks.Max(m => m.Percentage);
                if (mine.Percentage < best) continue;
                var shared = marks.Count(m => m.Percentage == best) > 1;
                var code = data.Modules.FirstOrDefault(m => m.ModuleId == assessment.ModuleId)?.Code;
                yield return new Award(TopMark, $"{(shared ? "Joint top" : "Top")} mark in {assessment.Name}",
                    $"{code} · {MarkRules.Format(mine.Score)}/{MarkRules.Format(mine.MaxScore)}");
            }
        }

        private static IEnumerable<Award> OnTimeAwards(StudentAwardData data, DateTime today)
        {
            var submitted = data.Submissions.Where(s => s.SubmittedAt != null).ToDictionary(s => s.AssessmentId, s => s.SubmittedAt);
            foreach (var term in data.Modules.Select(m => m.Term).Distinct().Order())
            {
                var moduleIds = data.Modules.Where(m => m.Term == term).Select(m => m.ModuleId).ToHashSet();
                var due = data.Assessments.Where(a => moduleIds.Contains(a.ModuleId) && a.DueDate.Date < today.Date).ToList();
                var allOnTime = due.TrueForAll(a => submitted.TryGetValue(a.AssessmentId, out var at) && Submission.StatusFor(at, a.DueDate) == "Submitted");
                if (due.Count == 0 || !allOnTime) continue;

                var termName = term == "Term2" ? "Term 2" : "Term 1";
                yield return new Award(OnTime, $"Every {termName} submission on time",
                    string.Create(CultureInfo.InvariantCulture, $"{due.Count} of {due.Count} handed in by the due date"));
            }
        }

        private static IEnumerable<Award> DistinctionAwards(StudentAwardData data) =>
            from module in data.Modules.OrderBy(m => m.Code)
            let marks = data.ClassMarks.Where(m => m.StudentId == data.StudentId && m.ModuleId == module.ModuleId && m.IsVisibleToStudent).ToList()
            where marks.Count >= MinimumMarks
            let average = RiskCalculator.AveragePercentage(marks)
            where average >= DistinctionPercent
            select new Award(Distinction, $"Distinction in {module.Code}",
                string.Create(CultureInfo.InvariantCulture, $"Average {average:0.#}% over {marks.Count} marks"));
    }
}
