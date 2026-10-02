using System.Net;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ISMLTS.Tests
{
    public class StudentPortalTests : IDisposable
    {
        private readonly SqliteTestDb _db = new();
        private readonly string _uploads = Path.Combine(Path.GetTempPath(), "ismlts-tests", Guid.NewGuid().ToString("N"));

        private StudentPortalService Service(ApplicationDbContext context) => new(
            new StudentRepository(context),
            new AssessmentRepository(context),
            new SubmissionRepository(context),
            new MarkRepository(context),
            new AttendanceRepository(context),
            new TicketRepository(context),
            new NotificationService(new NotificationRepository(context), new NotificationSettingRepository(context),
                new StudentRepository(context), new LecturerRepository(context), new NullEmailSender(), Options.Create(new EmailOptions())),
            new AttendanceVerifier(Options.Create(new AttendanceOptions { RadiusMeters = 200, AllowedIpRanges = { "10.0.0.0/8" } })),
            new SubmissionFileService(new SubmissionFileRepository(context), new LocalFileStore(_uploads), TimeProvider.System,
                NullLogger<SubmissionFileService>.Instance, Options.Create(new SubmissionFileOptions())),
            TimeProvider.System,
            new TermService(new TermRepository(context), TimeProvider.System),
            Options.Create(new RiskOptions()));

        private sealed record Seeded(int StudentId, int OutsiderId, int ModuleId, int AssessmentId, string OpenCode, string ClosedCode);

        private async Task<Seeded> SeedAsync()
        {
            await using var context = _db.NewContext();
            var lecturer = new Lecturer { FullName = "Lecturer", Email = "l@lecturers.test", PasswordHash = "x" };
            var module = new Module { Code = "PROG6211", Name = "Programming", Lecturer = lecturer };
            var student = new Student { FullName = "Thandi", Email = "st1@rcconnect.edu.za", PasswordHash = "x", Modules = { module } };
            var outsider = new Student { FullName = "Outsider", Email = "st2@rcconnect.edu.za", PasswordHash = "x" };
            var assessment = new Assessment { Module = module, Name = "ICE 1", DueDate = DateTime.Today.AddDays(3) };
            context.AddRange(student, outsider, assessment,
                new AttendanceSession { Module = module, Code = "OPEN01", StartedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(10), Latitude = -26.1455, Longitude = 28.0436 },
                new AttendanceSession { Module = module, Code = "SHUT01", StartedAt = DateTime.UtcNow.AddHours(-2), ExpiresAt = DateTime.UtcNow.AddHours(-1), IsClosed = true });
            await context.SaveChangesAsync();
            return new Seeded(student.StudentId, outsider.StudentId, module.ModuleId, assessment.AssessmentId, "OPEN01", "SHUT01");
        }

        [Fact]
        public async Task Scan_GivesACodeForEveryOutcome()
        {
            var data = await SeedAsync();
            await using var context = _db.NewContext();
            var portal = Service(context);
            var campus = IPAddress.Parse("10.1.2.3");

            Assert.Equal(ScanCodes.UnknownCode, (await portal.ScanAsync(data.StudentId, "NOPE99", campus, null, null, null)).Code);
            Assert.Equal(ScanCodes.Closed, (await portal.ScanAsync(data.StudentId, data.ClosedCode, campus, null, null, null)).Code);
            Assert.Equal(ScanCodes.NotEnrolled, (await portal.ScanAsync(data.OutsiderId, data.OpenCode, campus, null, null, null)).Code);
            Assert.Equal(ScanCodes.NotInClass, (await portal.ScanAsync(data.StudentId, data.OpenCode, IPAddress.Parse("8.8.8.8"), -33.9, 18.4, 10)).Code);

            var present = await portal.ScanAsync(data.StudentId, " open01 ", IPAddress.Parse("8.8.8.8"), -26.1456, 28.0438, 12);
            Assert.True(present.Present);
            Assert.Equal("You're marked present for PROG6211.", present.Message);

            Assert.Equal(ScanCodes.AlreadyPresent, (await portal.ScanAsync(data.StudentId, data.OpenCode, campus, null, null, null)).Code);
        }

        [Fact]
        public async Task Submit_OnlyTakesWebLinks_ForTheStudentsOwnModules()
        {
            var data = await SeedAsync();
            await using var context = _db.NewContext();
            var portal = Service(context);

            Assert.Equal(SubmitOutcome.NotFound, (await portal.SubmitAsync(data.OutsiderId, data.AssessmentId, "https://github.com/x")).Outcome);
            Assert.Equal(SubmitOutcome.BadLink, (await portal.SubmitAsync(data.StudentId, data.AssessmentId, "javascript:alert(1)")).Outcome);
            Assert.Equal(SubmitOutcome.Saved, (await portal.SubmitAsync(data.StudentId, data.AssessmentId, " https://github.com/me/ice ")).Outcome);
            Assert.Equal(SubmitOutcome.Saved, (await portal.SubmitAsync(data.StudentId, data.AssessmentId, "https://github.com/me/ice-v2")).Outcome);

            await using var check = _db.NewContext();
            var submission = await check.Submissions.SingleAsync();
            Assert.Equal("https://github.com/me/ice-v2", submission.Link);
        }

        [Theory]
        [InlineData(true, "", "Question", "Subject")]
        [InlineData(true, "Help", "", "Description")]
        [InlineData(false, "Help", "Question", "ModuleId")]
        public async Task RaiseTicket_ChecksEachField(bool ownModule, string subject, string description, string field)
        {
            var data = await SeedAsync();
            await using var context = _db.NewContext();

            var result = await Service(context).RaiseTicketAsync(ownModule ? data.StudentId : data.OutsiderId, data.ModuleId, subject, description);

            Assert.Null(result.Ticket);
            Assert.Equal(field, result.Field);
        }

        [Fact]
        public async Task RaiseTicket_SavesAndTellsTheLecturer()
        {
            var data = await SeedAsync();
            await using var context = _db.NewContext();

            var result = await Service(context).RaiseTicketAsync(data.StudentId, data.ModuleId, " Help ", "Where is the brief?");

            Assert.Equal("Help", result.Ticket?.Subject);
            await using var check = _db.NewContext();
            Assert.True(await check.Notifications.AnyAsync(n => n.Role == Roles.Lecturer));
        }

        [Fact]
        public async Task Assessments_ShowOnlyTheStudentsModules_WithTheirStatus()
        {
            var data = await SeedAsync();
            await using var context = _db.NewContext();
            var portal = Service(context);
            await portal.SubmitAsync(data.StudentId, data.AssessmentId, "https://github.com/me/ice");

            var mine = await portal.AssessmentsAsync(data.StudentId);
            var theirs = await portal.AssessmentsAsync(data.OutsiderId);

            var row = Assert.Single(mine);
            Assert.Equal(("Submitted", "PROG6211"), (row.Status, row.ModuleCode));
            Assert.Empty(theirs);
        }

        [Fact]
        public async Task SubmitFile_KeepsEveryUpload_AndTheNewestCounts()
        {
            var data = await SeedAsync();
            await using var context = _db.NewContext();
            var portal = Service(context);

            Assert.Equal(SubmitOutcome.Saved, (await portal.SubmitAsync(data.StudentId, data.AssessmentId, "https://github.com/me/ice", TestFiles.Upload(TestFiles.Pdf(), "draft.pdf"))).Outcome);
            // The app's upload leaves the link alone
            Assert.Equal(SubmitOutcome.Saved, (await portal.SubmitAsync(data.StudentId, data.AssessmentId, null, TestFiles.Upload(TestFiles.Docx(), "C:\\Users\\me\\final.docx"), keepLink: true)).Outcome);

            await using var check = _db.NewContext();
            var submission = await check.Submissions.Include(s => s.Files).SingleAsync();
            Assert.Equal("https://github.com/me/ice", submission.Link);
            Assert.Equal(2, submission.Files.Count);
            Assert.Equal("final.docx", submission.LatestFile?.FileName);
            Assert.All(submission.Files, f => Assert.True(File.Exists(Path.Combine(_uploads, f.StoredName))));

            var row = Assert.Single(await Service(check).AssessmentsAsync(data.StudentId));
            Assert.Equal(("final.docx", 2), (row.File?.FileName, row.FileCount));
        }

        [Fact]
        public async Task SubmitFile_RejectsAFileThatIsntWhatItsNameSays_AndStoresNothing()
        {
            var data = await SeedAsync();
            await using var context = _db.NewContext();

            var result = await Service(context).SubmitAsync(data.StudentId, data.AssessmentId, null, TestFiles.Upload(TestFiles.Program(), "work.pdf"));

            Assert.Equal(SubmitOutcome.BadFile, result.Outcome);
            Assert.Contains("isn't a real PDF", result.Message);
            await using var check = _db.NewContext();
            Assert.False(await check.Submissions.AnyAsync());
            Assert.False(Directory.Exists(_uploads) && Directory.EnumerateFiles(_uploads, "*", SearchOption.AllDirectories).Any());
        }

        [Fact]
        public async Task Submit_NeedsAFileOrALink_AndAnEmptyLinkRemovesTheOldOne()
        {
            var data = await SeedAsync();
            await using var context = _db.NewContext();
            var portal = Service(context);

            Assert.Equal(SubmitOutcome.Missing, (await portal.SubmitAsync(data.StudentId, data.AssessmentId, "  ")).Outcome);
            Assert.Equal(SubmitOutcome.Missing, (await portal.SubmitAsync(data.StudentId, data.AssessmentId, null, null, keepLink: true)).Outcome);

            await portal.SubmitAsync(data.StudentId, data.AssessmentId, "https://github.com/me/ice", TestFiles.Upload(TestFiles.Pdf(), "work.pdf"));
            Assert.Equal(SubmitOutcome.Saved, (await portal.SubmitAsync(data.StudentId, data.AssessmentId, "")).Outcome);

            await using var check = _db.NewContext();
            var submission = await check.Submissions.Include(s => s.Files).SingleAsync();
            Assert.Null(submission.Link);
            Assert.Single(submission.Files);
        }

        [Theory]
        [InlineData(null, -30, SubmitOutcome.Saved)]
        [InlineData(0, 0, SubmitOutcome.Saved)]
        [InlineData(0, -1, SubmitOutcome.Closed)]
        [InlineData(2, -2, SubmitOutcome.Saved)]
        [InlineData(2, -3, SubmitOutcome.Closed)]
        public async Task Submit_FollowsTheAssessmentsLateWindow(int? lateDays, int dueInDays, SubmitOutcome expected)
        {
            var data = await SeedAsync();
            int assessmentId;
            await using (var setup = _db.NewContext())
            {
                var assessment = new Assessment { ModuleId = data.ModuleId, Name = "Late window", DueDate = DateTime.Today.AddDays(dueInDays), LateDays = lateDays };
                setup.Add(assessment);
                await setup.SaveChangesAsync();
                assessmentId = assessment.AssessmentId;
            }
            await using var context = _db.NewContext();

            var result = await Service(context).SubmitAsync(data.StudentId, assessmentId, "https://github.com/me/late");

            Assert.Equal(expected, result.Outcome);
            if (expected == SubmitOutcome.Closed) Assert.StartsWith("Submissions closed", result.Message);
        }

        public void Dispose()
        {
            _db.Dispose();
            if (Directory.Exists(_uploads)) Directory.Delete(_uploads, recursive: true);
        }
    }
}
