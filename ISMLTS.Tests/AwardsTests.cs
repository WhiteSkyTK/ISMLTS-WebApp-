using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class AwardsTests
    {
        private const int Me = 1;
        private const int Classmate = 2;
        private static readonly DateTime Today = new(2026, 10, 1);

        private static readonly Module Prog = new() { ModuleId = 1, Code = "PROG6211", Term = "Term1" };
        private static readonly Module Data = new() { ModuleId = 2, Code = "DATA6211", Term = "Term2" };

        private static Assessment Assessment(int id, Module module, int daysAgo, bool released = true) =>
            new() { AssessmentId = id, ModuleId = module.ModuleId, Name = $"Task {id}", DueDate = Today.AddDays(-daysAgo), MarksReleased = released };

        private static Mark MarkFor(int student, Assessment a, decimal score) =>
            new() { StudentId = student, ModuleId = a.ModuleId, AssessmentId = a.AssessmentId, Assessment = a, Score = score, MaxScore = 100 };

        private static Submission Handed(Assessment a, bool late = false) =>
            new() { StudentId = Me, AssessmentId = a.AssessmentId, SubmittedAt = a.DueDate.AddDays(late ? 1 : -1).AddHours(10).ToUniversalTime() };

        private static List<Award> Earn(
            IEnumerable<Assessment>? assessments = null,
            IEnumerable<Submission>? submissions = null,
            IEnumerable<Mark>? marks = null,
            Dictionary<int, int>? sessions = null,
            Dictionary<int, int>? attended = null) =>
            Awards.For(new StudentAwardData(
                Me,
                new[] { Prog, Data },
                (assessments ?? Array.Empty<Assessment>()).ToList(),
                (submissions ?? Array.Empty<Submission>()).ToList(),
                (marks ?? Array.Empty<Mark>()).ToList(),
                sessions ?? new Dictionary<int, int>(),
                attended ?? new Dictionary<int, int>()), Today);

        [Theory]
        [InlineData(20, 19, true)]
        [InlineData(20, 18, false)]
        [InlineData(2, 2, false)]
        public void Attendance_Needs95PercentOfAtLeastThreeClasses(int sessions, int attended, bool expected)
        {
            var awards = Earn(sessions: new() { [1] = sessions }, attended: new() { [1] = attended });

            Assert.Equal(expected, awards.Exists(a => a.Kind == Awards.Attendance && a.Title.Contains("PROG6211")));
        }

        [Fact]
        public void TopMark_GoesToTheHighestReleasedMark_AndTiesAreJoint()
        {
            var quiz = Assessment(10, Prog, 5);
            var test = Assessment(11, Prog, 3);

            var awards = Earn(new[] { quiz, test }, marks: new[]
            {
                MarkFor(Me, quiz, 90), MarkFor(Classmate, quiz, 80),
                MarkFor(Me, test, 70), MarkFor(Classmate, test, 70)
            });

            Assert.Contains(awards, a => a.Title == "Top mark in Task 10");
            Assert.Contains(awards, a => a.Title == "Joint top mark in Task 11");
        }

        [Fact]
        public void TopMark_IgnoresUnreleasedMarks_AndAssessmentsWithOneMark()
        {
            var hidden = Assessment(10, Prog, 5, released: false);
            var alone = Assessment(11, Prog, 3);

            var awards = Earn(new[] { hidden, alone }, marks: new[] { MarkFor(Me, hidden, 95), MarkFor(Classmate, hidden, 10), MarkFor(Me, alone, 99) });

            Assert.DoesNotContain(awards, a => a.Kind == Awards.TopMark);
        }

        [Fact]
        public void OnTime_NeedsEveryAssessmentDueSoFarInTheTerm_HandedInOnTime()
        {
            var first = Assessment(10, Prog, 20);
            var second = Assessment(11, Prog, 5);
            var future = Assessment(12, Prog, -7);
            var otherTerm = Assessment(20, Data, 5);

            var awards = Earn(new[] { first, second, future, otherTerm }, new[] { Handed(first), Handed(second), Handed(otherTerm, late: true) });

            Assert.Contains(awards, a => a.Title == "Every Term 1 submission on time");
            Assert.DoesNotContain(awards, a => a.Title.Contains("Term 2"));
        }

        [Fact]
        public void OnTime_IsNotEarnedWithAMissingSubmission_OrNothingDue()
        {
            var first = Assessment(10, Prog, 20);
            var second = Assessment(11, Prog, 5);

            Assert.DoesNotContain(Earn(new[] { first, second }, new[] { Handed(first) }), a => a.Kind == Awards.OnTime);
            Assert.DoesNotContain(Earn(new[] { Assessment(12, Prog, -3) }), a => a.Kind == Awards.OnTime);
        }

        [Fact]
        public void Distinction_NeedsA75PercentAverage_OverTwoReleasedMarks()
        {
            var a = Assessment(10, Prog, 10);
            var b = Assessment(11, Prog, 5);
            var c = Assessment(20, Data, 5);
            var hidden = Assessment(21, Data, 2, released: false);

            var awards = Earn(new[] { a, b, c, hidden }, marks: new[]
            {
                MarkFor(Me, a, 80), MarkFor(Me, b, 72),
                MarkFor(Me, c, 90), MarkFor(Me, hidden, 95)
            });

            Assert.Contains(awards, x => x.Title == "Distinction in PROG6211" && x.Detail == "Average 76% over 2 marks");
            Assert.DoesNotContain(awards, x => x.Title == "Distinction in DATA6211");
        }

        [Fact]
        public void EveryKind_HasATitleAndHowToEarnIt()
        {
            Assert.Equal(4, Awards.Kinds.Count);
            Assert.All(Awards.Kinds, k => Assert.False(string.IsNullOrWhiteSpace(k.HowToEarn)));
        }
    }
}
