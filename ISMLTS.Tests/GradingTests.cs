using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class GradingTests
    {
        private static readonly HashSet<int> Enrolled = new() { 1, 2, 3 };

        [Fact]
        public void ValidateGradebook_AcceptsValidAndBlankRows()
        {
            var rows = new List<GradebookEntry>
            {
                new() { StudentId = 1, Score = 40 },
                new() { StudentId = 2, Score = null },
                new() { StudentId = 3, Score = 0, Feedback = "Nothing submitted" }
            };

            Assert.Empty(Grading.ValidateGradebook(rows, Enrolled, 50));
        }

        [Fact]
        public void ValidateGradebook_ReportsEachBadRowByIndex()
        {
            var rows = new List<GradebookEntry>
            {
                new() { StudentId = 1, Score = 51 },
                new() { StudentId = 2, Score = null, Feedback = "Feedback without a score" },
                new() { StudentId = 9, Score = 10 },
                new() { StudentId = 1, Score = 10 },
                new() { StudentId = 3, Score = 10, Feedback = new string('x', 1001) }
            };

            var errors = Grading.ValidateGradebook(rows, Enrolled, 50);

            Assert.Equal("The score can't be more than 50.", errors[0]);
            Assert.Equal("Enter a score to save feedback.", errors[1]);
            Assert.Equal("This student isn't enrolled in the module.", errors[2]);
            Assert.Equal("This student appears twice.", errors[3]);
            Assert.Equal("Keep feedback to 1000 characters.", errors[4]);
        }

        [Fact]
        public void QuickEvalQueue_SkipsMarkedAndUnsubmittedWork_OldestDueFirst()
        {
            var early = new Assessment { AssessmentId = 1, DueDate = new DateTime(2026, 9, 1) };
            var late = new Assessment { AssessmentId = 2, DueDate = new DateTime(2026, 10, 1) };
            var submissions = new List<Submission>
            {
                new() { SubmissionId = 1, AssessmentId = 2, Assessment = late, StudentId = 1, SubmittedAt = new DateTime(2026, 9, 20) },
                new() { SubmissionId = 2, AssessmentId = 1, Assessment = early, StudentId = 2, SubmittedAt = new DateTime(2026, 8, 30) },
                new() { SubmissionId = 3, AssessmentId = 1, Assessment = early, StudentId = 1, SubmittedAt = new DateTime(2026, 8, 29) },
                new() { SubmissionId = 4, AssessmentId = 1, Assessment = early, StudentId = 3, SubmittedAt = new DateTime(2026, 8, 28) },
                new() { SubmissionId = 5, AssessmentId = 2, Assessment = late, StudentId = 2, SubmittedAt = null }
            };
            var marked = new HashSet<(int, int)> { (1, 3) };

            var queue = Grading.QuickEvalQueue(submissions, marked);

            Assert.Equal(new[] { 3, 2, 1 }, queue.Select(s => s.SubmissionId));
        }
    }
}
