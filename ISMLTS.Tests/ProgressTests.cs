using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class ProgressTests
    {
        private static readonly DateTime Today = new(2026, 10, 1);
        private static readonly Module Wil = new() { ModuleId = 1, Code = "XADAD7112", Name = "WIL" };

        private static Mark MarkOf(decimal score, decimal max = 100) => new() { AssessmentName = "x", Score = score, MaxScore = max };

        private static Assessment Due(int id, int daysFromToday) => new() { AssessmentId = id, ModuleId = 1, Name = $"A{id}", DueDate = Today.AddDays(daysFromToday) };

        [Theory]
        [InlineData(0, 0, null)]
        [InlineData(3, 4, 75)]
        [InlineData(2, 3, 67)]
        [InlineData(1, 8, 13)]
        public void Percent_RoundsToWholeNumbers(int part, int whole, int? expected) =>
            Assert.Equal(expected, Progress.Percent(part, whole));

        [Fact]
        public void RiskReasons_CoverLowAveragesAndLowAttendance()
        {
            Assert.Empty(Progress.RiskReasons(50m, 75, 75));
            Assert.Empty(Progress.RiskReasons(null, null, 75));
            Assert.Equal(new[] { "Average 49.9% is below 50%" }, Progress.RiskReasons(49.9m, 90, 75));
            Assert.Equal(new[] { "Attendance 74% is below 75%" }, Progress.RiskReasons(null, 74, 75));
            Assert.Equal(2, Progress.RiskReasons(10m, 10, 75).Count);
        }

        [Fact]
        public void ForModule_WorksOutAverageAttendanceSubmissionsAndNextDue()
        {
            var data = new StudentModuleData(
                Wil,
                new[] { MarkOf(40, 50), MarkOf(60) },           // 80% and 60%
                SessionsHeld: 8,
                SessionsAttended: 5,
                new[] { Due(1, -3), Due(2, 2), Due(3, 9) },
                new HashSet<int> { 1, 2 });

            var progress = Progress.ForModule(data, Today, 75);

            Assert.Equal(70m, progress.Average);
            Assert.Equal(63, progress.AttendancePercent);
            Assert.Equal((2, 3), (progress.Submitted, progress.AssessmentCount));
            Assert.Equal(3, progress.NextDue?.AssessmentId);
            Assert.True(progress.AtRisk);
            Assert.Equal(new[] { "Attendance 63% is below 75%" }, progress.RiskReasons);
        }

        [Fact]
        public void ForModule_WithNothingYet_IsNotAtRisk()
        {
            var progress = Progress.ForModule(new StudentModuleData(Wil, Array.Empty<Mark>(), 0, 0, Array.Empty<Assessment>(), new HashSet<int>()), Today, 75);

            Assert.Null(progress.Average);
            Assert.Null(progress.AttendancePercent);
            Assert.Null(progress.NextDue);
            Assert.False(progress.AtRisk);
        }

        [Fact]
        public void Summarise_AveragesModulesWithMarks_AndPoolsAttendance()
        {
            var modules = new[]
            {
                new ModuleProgress(1, "A", "A", 80m, 100, 4, 4, 2, 3, null, Array.Empty<string>()),
                new ModuleProgress(2, "B", "B", 45m, 50, 2, 4, 1, 2, null, new[] { "Average 45% is below 50%" }),
                new ModuleProgress(3, "C", "C", null, null, 0, 0, 0, 1, null, Array.Empty<string>())
            };

            var summary = Progress.Summarise(modules);

            Assert.Equal(new ProgressSummary(62.5m, 75, 3, 6, 1, 3), summary);
        }
    }
}
