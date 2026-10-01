using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class PortfolioTests
    {
        private static readonly Module Term1 = new() { ModuleId = 1, Code = "PROG6211", Name = "Programming", Term = "Term1" };
        private static readonly Module Term2 = new() { ModuleId = 2, Code = "DATA6211", Name = "Databases", Term = "Term2" };

        private static readonly Assessment Released = new() { AssessmentId = 10, ModuleId = 1, Name = "ICE 1", Type = "ICE", DueDate = new DateTime(2026, 9, 1), MarksReleased = true };
        private static readonly Assessment Hidden = new() { AssessmentId = 11, ModuleId = 1, Name = "Quiz 1", Type = "Quiz", DueDate = new DateTime(2026, 9, 20) };
        private static readonly Assessment Untouched = new() { AssessmentId = 12, ModuleId = 1, Name = "POE", Type = "POE", DueDate = new DateTime(2026, 10, 20) };

        private static Submission Handed(Assessment a, string link = "https://github.com/me/work") =>
            new() { AssessmentId = a.AssessmentId, StudentId = 5, SubmittedAt = a.DueDate.AddDays(-1).ToUniversalTime(), Link = link };

        private static Mark MarkFor(Assessment? a, decimal score, int moduleId = 1) => new()
        {
            MarkId = (int)score,
            StudentId = 5,
            ModuleId = moduleId,
            AssessmentId = a?.AssessmentId,
            Assessment = a,
            AssessmentName = a?.Name ?? "Class test",
            Score = score,
            MaxScore = 100,
            Feedback = "Nice"
        };

        [Fact]
        public void HandedInWork_ShowsTheMarkOnlyOnceReleased()
        {
            var modules = Portfolio.Build(new[] { Term1 }, new[] { Released, Hidden, Untouched },
                new[] { Handed(Released), Handed(Hidden) }, new[] { MarkFor(Released, 70), MarkFor(Hidden, 40) });

            var entries = modules.Single().Entries;
            Assert.Equal(new[] { "Quiz 1", "ICE 1" }, entries.Select(e => e.Name));
            Assert.Null(entries[0].Mark);
            Assert.True(entries[0].WaitingForMarks);
            Assert.Equal(70m, entries[1].Mark?.Score);
            Assert.False(entries[1].WaitingForMarks);
            Assert.Equal(70m, modules.Single().Average);
        }

        [Fact]
        public void WorkNeverHandedInOrMarked_IsLeftOut()
        {
            var modules = Portfolio.Build(new[] { Term1 }, new[] { Untouched }, Array.Empty<Submission>(), Array.Empty<Mark>());

            Assert.Empty(modules.Single().Entries);
            Assert.Null(modules.Single().Average);
        }

        [Fact]
        public void ReleasedMarks_WithoutASubmission_StillShow()
        {
            var entries = Portfolio.Build(new[] { Term1 }, new[] { Released }, Array.Empty<Submission>(), new[] { MarkFor(Released, 55) }).Single().Entries;

            var entry = Assert.Single(entries);
            Assert.Null(entry.SubmittedAt);
            Assert.Equal(55m, entry.Mark?.Score);
        }

        [Fact]
        public void MarksNotTiedToAnAssessment_ShowAsOther()
        {
            var entries = Portfolio.Build(new[] { Term1 }, Array.Empty<Assessment>(), Array.Empty<Submission>(), new[] { MarkFor(null, 62) }).Single().Entries;

            var entry = Assert.Single(entries);
            Assert.Equal(("Class test", "Other"), (entry.Name, entry.Type));
        }

        [Fact]
        public void LinksThatAreNotWebAddresses_AreDropped()
        {
            var entries = Portfolio.Build(new[] { Term1 }, new[] { Released }, new[] { Handed(Released, "javascript:alert(1)") }, Array.Empty<Mark>()).Single().Entries;

            Assert.Null(Assert.Single(entries).Link);
        }

        [Fact]
        public void Modules_AreInTermThenCodeOrder()
        {
            var modules = Portfolio.Build(new[] { Term2, Term1 }, Array.Empty<Assessment>(), Array.Empty<Submission>(), Array.Empty<Mark>());

            Assert.Equal(new[] { "PROG6211", "DATA6211" }, modules.Select(m => m.Code));
        }
    }
}
