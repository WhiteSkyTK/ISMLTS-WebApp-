using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ISMLTS.Tests
{
    public class WeeklyDigestTests : IDisposable
    {
        // Monday 5 October 2026, 08:00 SAST
        private static readonly DateTime MondayMorningUtc = new(2026, 10, 5, 6, 0, 0, DateTimeKind.Utc);

        private readonly SqliteTestDb _db = new();
        private readonly FakeEmailSender _email = new();

        [Theory]
        [InlineData(0, null, true)]            // Monday 08:00, never sent
        [InlineData(-2, null, false)]          // Monday 06:00, too early
        [InlineData(24, null, false)]          // Tuesday
        [InlineData(0, -1, false)]             // already sent an hour ago
        [InlineData(0, -24 * 7, true)]         // last sent a week ago
        public void IsDue_MondayFrom7amSast_OncePerWeek(int hoursFromMondayEight, int? lastSentHoursAgo, bool expected)
        {
            var now = MondayMorningUtc.AddHours(hoursFromMondayEight);
            DateTime? lastSent = lastSentHoursAgo is int ago ? now.AddHours(ago) : null;
            Assert.Equal(expected, WeeklyDigest.IsDue(now, lastSent));
        }

        [Fact]
        public void Build_ListsEachAssessmentWithItsModuleAndDay()
        {
            var text = WeeklyDigest.Build("Thandi", new[]
            {
                new DigestItem("SOEN7112", "Quiz 2", new DateTime(2026, 10, 8)),
                new DigestItem("XADAD7112", "POE Part 1", new DateTime(2026, 10, 6))
            }, "https://ismlts.example/Assessments/MyAssessments");

            Assert.StartsWith("Hi Thandi,", text);
            Assert.Contains("You have 2 assessments due this week:", text);
            Assert.True(text.IndexOf("Tue 06 Oct: POE Part 1 (XADAD7112)", StringComparison.Ordinal) < text.IndexOf("Thu 08 Oct: Quiz 2 (SOEN7112)", StringComparison.Ordinal));
            Assert.Contains("https://ismlts.example/Assessments/MyAssessments", text);
        }

        [Fact]
        public async Task Sender_EmailsStudentsWithUnsubmittedWorkOnce_AndRespectsOptOut()
        {
            int keenId, doneId, optedOutId;
            await using (var setup = _db.NewContext())
            {
                var lecturer = new Lecturer { FullName = "Lecturer", Email = "lecturer@lecturers.test", PasswordHash = "x" };
                var module = new Module { Code = "XADAD7112", Name = "WIL", Lecturer = lecturer };
                var poe = new Assessment { Module = module, Name = "POE Part 1", Type = "POE", DueDate = new DateTime(2026, 10, 7) };
                var later = new Assessment { Module = module, Name = "POE Part 2", Type = "POE", DueDate = new DateTime(2026, 11, 20) };
                var keen = new Student { FullName = "Keen", Email = "keen@students.test", Modules = { module } };
                var done = new Student { FullName = "Done", Email = "done@students.test", Modules = { module } };
                var optedOut = new Student { FullName = "Quiet", Email = "quiet@students.test", Modules = { module } };
                setup.AddRange(poe, later, keen, done, optedOut);
                await setup.SaveChangesAsync();
                setup.Submissions.Add(new Submission { AssessmentId = poe.AssessmentId, StudentId = done.StudentId, SubmittedAt = MondayMorningUtc, Link = "https://github.com/x" });
                setup.NotificationSettings.Add(new NotificationSetting { Role = Roles.Student, UserId = optedOut.StudentId, WeeklyDigest = false });
                await setup.SaveChangesAsync();
                (keenId, doneId, optedOutId) = (keen.StudentId, done.StudentId, optedOut.StudentId);
            }

            Assert.Equal(1, await RunAsync(MondayMorningUtc));
            var email = Assert.Single(_email.Sent);
            Assert.Equal("keen@students.test", email.To);
            Assert.Contains("POE Part 1", email.Body);
            Assert.DoesNotContain("POE Part 2", email.Body);

            Assert.Equal(0, await RunAsync(MondayMorningUtc.AddMinutes(30)));

            await using var check = _db.NewContext();
            Assert.NotNull((await check.NotificationSettings.SingleAsync(s => s.UserId == keenId)).LastDigestSentAt);
            Assert.False(await check.NotificationSettings.AnyAsync(s => s.UserId == doneId));
            Assert.Null((await check.NotificationSettings.SingleAsync(s => s.UserId == optedOutId)).LastDigestSentAt);
        }

        private async Task<int> RunAsync(DateTime nowUtc)
        {
            await using var context = _db.NewContext();
            var sender = new WeeklyDigestSender(
                new StudentRepository(context),
                new AssessmentRepository(context),
                new SubmissionRepository(context),
                new NotificationSettingRepository(context),
                _email,
                Options.Create(new EmailOptions { SiteUrl = "https://ismlts.example" }));
            return await sender.SendDueAsync(nowUtc);
        }

        public void Dispose() => _db.Dispose();
    }
}
