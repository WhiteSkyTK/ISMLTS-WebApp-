using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class ClassInsightsTests
    {
        private static readonly DateTime Today = new(2026, 10, 1);

        [Fact]
        public void ForModule_WorksOutClassRates_AndListsWhoIsAtRisk()
        {
            var keen = new Student { StudentId = 1, FullName = "Keen" };
            var struggling = new Student { StudentId = 2, FullName = "Struggling" };
            var absent = new Student { StudentId = 3, FullName = "Absent" };
            var module = new Module { ModuleId = 7, Code = "XADAD7112", Name = "WIL", Students = { keen, struggling, absent } };

            var dueSoFar = new Assessment { AssessmentId = 10, ModuleId = 7, DueDate = Today.AddDays(-2) };
            var notDueYet = new Assessment { AssessmentId = 11, ModuleId = 7, DueDate = Today.AddDays(5) };
            var data = new ClassModuleData(
                module,
                new[]
                {
                    new Mark { StudentId = 1, Score = 80, MaxScore = 100 },
                    new Mark { StudentId = 2, Score = 20, MaxScore = 50 },   // 40%
                    new Mark { StudentId = 3, Score = 60, MaxScore = 100 }
                },
                SessionsHeld: 4,
                new[]
                {
                    new AttendanceRecord { StudentId = 1, SessionId = 1 }, new AttendanceRecord { StudentId = 1, SessionId = 2 },
                    new AttendanceRecord { StudentId = 1, SessionId = 3 }, new AttendanceRecord { StudentId = 1, SessionId = 4 },
                    new AttendanceRecord { StudentId = 2, SessionId = 1 }, new AttendanceRecord { StudentId = 2, SessionId = 2 },
                    new AttendanceRecord { StudentId = 2, SessionId = 3 }
                },
                new[] { dueSoFar, notDueYet },
                new[]
                {
                    new Submission { StudentId = 1, AssessmentId = 10, SubmittedAt = Today },
                    new Submission { StudentId = 1, AssessmentId = 11, SubmittedAt = Today },
                    new Submission { StudentId = 2, AssessmentId = 10, SubmittedAt = Today }
                });

            var insight = ClassInsights.ForModule(data, Today, 75);

            Assert.Equal(3, insight.Enrolled);
            Assert.Equal(60m, insight.ClassAverage);          // (80 + 40 + 60) / 3
            Assert.Equal(58, insight.AttendanceRate);         // 7 of 12 student-sessions
            Assert.Equal(67, insight.SubmissionRate);         // 2 of 3 due hand-ins
            Assert.Equal(new[] { "Struggling", "Absent" }, insight.AtRisk.Select(s => s.FullName));
            Assert.Equal(new[] { "Average 40% is below 50%" }, insight.AtRisk[0].RiskReasons);
            Assert.Equal(new[] { "Attendance 0% is below 75%" }, insight.AtRisk[1].RiskReasons);
            Assert.Equal((1, 1), (insight.AtRisk[0].Submitted, insight.AtRisk[0].DueSoFar));
        }

        [Fact]
        public void ForModule_WithNoStudentsOrSessions_HasNoRates()
        {
            var insight = ClassInsights.ForModule(
                new ClassModuleData(new Module { ModuleId = 1, Code = "EMPTY", Name = "Empty" }, Array.Empty<Mark>(), 0,
                    Array.Empty<AttendanceRecord>(), Array.Empty<Assessment>(), Array.Empty<Submission>()),
                Today, 75);

            Assert.Equal((0, (decimal?)null, (int?)null, (int?)null), (insight.Enrolled, insight.ClassAverage, insight.AttendanceRate, insight.SubmissionRate));
            Assert.Empty(insight.AtRisk);
        }
    }
}
