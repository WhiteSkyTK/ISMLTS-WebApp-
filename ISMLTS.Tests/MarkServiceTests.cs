using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ISMLTS.Tests
{
    public class MarkServiceTests : IDisposable
    {
        private static readonly Grader Lecturer = new(1, "Lecturer One");

        private readonly SqliteTestDb _db = new();
        private readonly int _moduleId, _studentId, _assessmentId;

        public MarkServiceTests()
        {
            using var context = _db.NewContext();
            var lecturer = new Lecturer { FullName = "Lecturer One", Email = "one@lecturers.test", PasswordHash = "x" };
            var module = new Module { Code = "XADAD7112", Name = "WIL", Lecturer = lecturer };
            var student = new Student { FullName = "Student", Email = "student@students.test", Modules = { module } };
            var assessment = new Assessment { Module = module, Name = "POE Part 1", Type = "POE", DueDate = new DateTime(2026, 10, 9), MaxScore = 50 };
            context.AddRange(student, assessment);
            context.SaveChanges();
            (_moduleId, _studentId, _assessmentId) = (module.ModuleId, student.StudentId, assessment.AssessmentId);
        }

        [Theory]
        [InlineData(null, "Enter a score.")]
        [InlineData(-1.0, "The score can't be negative.")]
        [InlineData(50.5, "The score can't be more than 50.")]
        [InlineData(50.0, null)]
        [InlineData(0.0, null)]
        public void MarkRules_CheckScore(double? score, string? expected) =>
            Assert.Equal(expected, MarkRules.CheckScore(score is double s ? (decimal)s : null, 50));

        [Theory]
        [InlineData(null, "Enter the total it's out of.")]
        [InlineData(0.0, "The total must be more than 0.")]
        [InlineData(1001.0, "The total can't be more than 1000.")]
        [InlineData(100.0, null)]
        public void MarkRules_CheckOutOf(double? outOf, string? expected) =>
            Assert.Equal(expected, MarkRules.CheckOutOf(outOf is double o ? (decimal)o : null));

        [Fact]
        public void MarkRules_CheckFeedback_LimitsLength()
        {
            Assert.Null(MarkRules.CheckFeedback(new string('a', 1000)));
            Assert.NotNull(MarkRules.CheckFeedback(new string('a', 1001)));
        }

        private MarkService Service(ApplicationDbContext context) => new(
            new MarkRepository(context),
            new MarkChangeRepository(context),
            new AssessmentRepository(context),
            new NotificationService(
                new NotificationRepository(context), new NotificationSettingRepository(context),
                new StudentRepository(context), new LecturerRepository(context),
                new NullEmailSender(), Options.Create(new EmailOptions())));

        private async Task<Mark> SaveAsync(decimal score, string? feedback = null)
        {
            await using var context = _db.NewContext();
            var module = await context.Modules.FindAsync(_moduleId) ?? throw new InvalidOperationException();
            var assessment = await context.Assessments.FindAsync(_assessmentId) ?? throw new InvalidOperationException();
            return await Service(context).SaveAsync(module,
                new MarkEntry(_studentId, assessment, assessment.Name, score, assessment.MaxScore, feedback, new DateTime(2026, 10, 12)),
                Lecturer, MarkSources.Gradebook);
        }

        [Fact]
        public async Task SavingTwiceForTheSameAssessment_UpdatesOneMark_AndAuditsBoth()
        {
            await SaveAsync(30);
            await SaveAsync(35, "Better structure needed");

            await using var check = _db.NewContext();
            var mark = await check.Marks.SingleAsync();
            Assert.Equal((35m, 50m, "Better structure needed"), (mark.Score, mark.MaxScore, mark.Feedback));

            var history = await check.MarkChanges.OrderBy(c => c.MarkChangeId).ToListAsync();
            Assert.Equal(2, history.Count);
            Assert.Equal((MarkChangeActions.Created, (decimal?)null, (decimal?)30m), (history[0].Action, history[0].OldScore, history[0].NewScore));
            Assert.Equal((MarkChangeActions.Updated, (decimal?)30m, (decimal?)35m, true), (history[1].Action, history[1].OldScore, history[1].NewScore, history[1].FeedbackChanged));
            Assert.All(history, c => Assert.Equal(("Lecturer One", MarkSources.Gradebook), (c.ChangedByName, c.Source)));
        }

        [Fact]
        public async Task SavingTheSameValuesAgain_IsNotAudited()
        {
            await SaveAsync(30);
            await SaveAsync(30);

            await using var check = _db.NewContext();
            Assert.Equal(1, await check.MarkChanges.CountAsync());
        }

        [Fact]
        public async Task UnreleasedMarks_DoNotNotify_UntilTheAssessmentIsReleased()
        {
            await SaveAsync(40);
            await using (var check = _db.NewContext())
            {
                Assert.False(await check.Notifications.AnyAsync());
            }

            await using (var context = _db.NewContext())
            {
                var assessment = await context.Assessments.FindAsync(_assessmentId) ?? throw new InvalidOperationException();
                var module = await context.Modules.FindAsync(_moduleId) ?? throw new InvalidOperationException();
                await Service(context).SetReleasedAsync(assessment, module, released: true);
            }

            await using var after = _db.NewContext();
            var notification = await after.Notifications.SingleAsync();
            Assert.Equal("Marks released: POE Part 1", notification.Title);
            Assert.Equal("XADAD7112 · 40/50 (80%)", notification.Message);
            Assert.True((await after.Assessments.FindAsync(_assessmentId))!.MarksReleased);
        }

        [Fact]
        public async Task DeletingAMark_LeavesAnAuditEntry()
        {
            var saved = await SaveAsync(20);
            await using (var context = _db.NewContext())
            {
                var mark = await context.Marks.FindAsync(saved.MarkId) ?? throw new InvalidOperationException();
                await Service(context).DeleteAsync(mark, Lecturer);
            }

            await using var check = _db.NewContext();
            Assert.False(await check.Marks.AnyAsync());
            var deleted = await check.MarkChanges.SingleAsync(c => c.Action == MarkChangeActions.Deleted);
            Assert.Equal((saved.MarkId, (decimal?)20m), (deleted.MarkId, deleted.OldScore));
        }

        [Fact]
        public async Task DeletingAnAssessment_KeepsItsMarks()
        {
            await SaveAsync(25);
            await using (var context = _db.NewContext())
            {
                var assessment = await context.Assessments.FindAsync(_assessmentId) ?? throw new InvalidOperationException();
                await Service(context).DeleteAssessmentAsync(assessment);
            }

            await using var check = _db.NewContext();
            var mark = await check.Marks.SingleAsync();
            Assert.Null(mark.AssessmentId);
            Assert.Equal("POE Part 1", mark.AssessmentName);
            Assert.False(await check.Assessments.AnyAsync());
        }

        public void Dispose() => _db.Dispose();
    }
}
