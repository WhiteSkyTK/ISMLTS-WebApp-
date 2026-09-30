using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class MarkbookTests
    {
        private static readonly Module Module = new() { ModuleId = 1, Code = "PROG6211", Name = "Programming 2A" };
        private static readonly Student Zanele = new() { StudentId = 1, FullName = "Zanele Nkosi", Email = "st1@rcconnect.edu.za" };
        private static readonly Student Ayanda = new() { StudentId = 2, FullName = "Ayanda Zulu", Email = "st2@rcconnect.edu.za" };
        private static readonly Student Left = new() { StudentId = 3, FullName = "Left The Module", Email = "st3@rcconnect.edu.za" };

        private static readonly Assessment Quiz = new() { AssessmentId = 10, ModuleId = 1, Name = "Quiz 1", DueDate = new DateTime(2026, 9, 20), MaxScore = 30 };
        private static readonly Assessment Ice = new() { AssessmentId = 11, ModuleId = 1, Name = "ICE 1", DueDate = new DateTime(2026, 9, 1), MaxScore = 20, MarksReleased = true };

        private static Mark MarkFor(Student student, Assessment? assessment, decimal score, decimal outOf, int id) => new()
        {
            MarkId = id,
            StudentId = student.StudentId,
            ModuleId = 1,
            AssessmentId = assessment?.AssessmentId,
            Assessment = assessment,
            AssessmentName = assessment?.Name ?? "Class test",
            Score = score,
            MaxScore = outOf
        };

        private static MarkbookViewModel Build(IEnumerable<Mark> marks, IEnumerable<Submission>? submissions = null) =>
            Markbook.Build(Module, new[] { Zanele, Ayanda }, new[] { Quiz, Ice }, marks, submissions ?? Array.Empty<Submission>());

        [Fact]
        public void ColumnsFollowTheDueDate_AndRowsTheStudentsName()
        {
            var markbook = Build(Array.Empty<Mark>());

            Assert.Equal(new[] { "ICE 1", "Quiz 1" }, markbook.Columns.Select(c => c.Name));
            Assert.Equal(new[] { "Ayanda Zulu", "Zanele Nkosi" }, markbook.Rows.Select(r => r.FullName));
            Assert.All(markbook.Rows, r => Assert.Equal(new[] { 11, 10 }, r.Cells.Select(c => c.AssessmentId)));
        }

        [Fact]
        public void EachMark_LandsInItsAssessmentsCell_OrUnderOther()
        {
            var markbook = Build(new[]
            {
                MarkFor(Zanele, Ice, 15, 20, 1),
                MarkFor(Zanele, Quiz, 12, 30, 2),
                MarkFor(Zanele, null, 40, 50, 3)
            });

            var zanele = markbook.Rows.Single(r => r.StudentId == Zanele.StudentId);
            Assert.Equal(1, zanele.Cells[0].Mark?.MarkId);
            Assert.Equal(2, zanele.Cells[1].Mark?.MarkId);
            Assert.Equal(3, Assert.Single(zanele.OtherMarks).MarkId);
            // (75 + 40 + 80) / 3
            Assert.Equal(65m, zanele.AveragePercentage);
            Assert.False(zanele.IsAtRisk);
        }

        [Fact]
        public void StudentsWithoutMarks_HaveNoAverage_AndAreNotAtRisk()
        {
            var ayanda = Build(Array.Empty<Mark>()).Rows.Single(r => r.StudentId == Ayanda.StudentId);

            Assert.Null(ayanda.AveragePercentage);
            Assert.False(ayanda.IsAtRisk);
            Assert.Equal(MarkbookStatuses.NoMarks, ayanda.StatusTokens);
        }

        [Fact]
        public void HandedInWorkWithoutAMark_IsWaitingToBeMarked()
        {
            var submissions = new[]
            {
                new Submission { StudentId = Ayanda.StudentId, AssessmentId = Quiz.AssessmentId, SubmittedAt = new DateTime(2026, 9, 22, 8, 0, 0, DateTimeKind.Utc) },
                new Submission { StudentId = Zanele.StudentId, AssessmentId = Quiz.AssessmentId, SubmittedAt = new DateTime(2026, 9, 19, 8, 0, 0, DateTimeKind.Utc) }
            };

            var markbook = Build(new[] { MarkFor(Zanele, Quiz, 10, 30, 1) }, submissions);

            var ayandaQuiz = markbook.Rows.Single(r => r.StudentId == Ayanda.StudentId).Cells[1];
            Assert.True(ayandaQuiz.IsWaitingForMark);
            Assert.Equal("Late", ayandaQuiz.SubmissionStatus);
            Assert.False(markbook.Rows.Single(r => r.StudentId == Zanele.StudentId).Cells[1].IsWaitingForMark);
            Assert.Equal(1, markbook.Columns[1].ToMarkCount);
            Assert.Equal(1, markbook.ToMarkCount);
            Assert.Equal($"{MarkbookStatuses.NoMarks} {MarkbookStatuses.ToMark}", markbook.Rows.Single(r => r.StudentId == Ayanda.StudentId).StatusTokens);
        }

        [Fact]
        public void AtRiskStudents_AreCounted_AndTagged()
        {
            var markbook = Build(new[] { MarkFor(Ayanda, Ice, 8, 20, 1), MarkFor(Zanele, Ice, 18, 20, 2) });

            Assert.Equal(1, markbook.AtRiskCount);
            Assert.Equal(MarkbookStatuses.AtRisk, markbook.Rows.Single(r => r.StudentId == Ayanda.StudentId).StatusTokens);
            Assert.Equal(MarkbookStatuses.OnTrack, markbook.Rows.Single(r => r.StudentId == Zanele.StudentId).StatusTokens);
            Assert.Equal(65m, markbook.Columns[0].AveragePercentage);
        }

        [Fact]
        public void HiddenMarks_CountOnlyUnreleasedAssessments()
        {
            var markbook = Build(new[] { MarkFor(Ayanda, Ice, 8, 20, 1), MarkFor(Ayanda, Quiz, 20, 30, 2), MarkFor(Zanele, Quiz, 25, 30, 3) });

            Assert.Equal(2, markbook.HiddenMarkCount);
            Assert.Equal((1, 2), (markbook.Columns[0].MarkedCount, markbook.Columns[1].MarkedCount));
        }

        [Fact]
        public void MarksOfStudentsWhoLeftTheModule_AreIgnored()
        {
            var markbook = Build(new[] { MarkFor(Left, Ice, 1, 20, 1) });

            Assert.DoesNotContain(markbook.Rows, r => r.StudentId == Left.StudentId);
            Assert.Equal(0, markbook.Columns[0].MarkedCount);
            Assert.Null(markbook.Columns[0].AveragePercentage);
        }

        [Theory]
        [InlineData(0, "Nobody has a mark yet")]
        [InlineData(1, "The student with a mark will see it")]
        [InlineData(7, "The 7 students with a mark will see them")]
        public void ReleaseConfirmation_SaysWhoWillSeeTheMarks(int marked, string expected)
        {
            var button = new ReleaseButtonModel { AssessmentName = "Quiz 1", MarkedCount = marked };

            Assert.Contains(expected, button.ConfirmMessage);
        }
    }
}
