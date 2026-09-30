using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ISMLTS.Tests
{
    public class DemoSeederTests : IDisposable
    {
        private readonly SqliteTestDb _db = new();

        private static IConfiguration Config(bool demo = true) =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:DemoData"] = demo ? "true" : "false",
                ["Seed:DemoPassword"] = "12345678"
            }).Build();

        private async Task SeedAsync(bool demo = true)
        {
            await using var context = _db.NewContext();
            await DemoSeeder.SeedAsync(context, Config(demo));
            await DataSeeder.SeedDataAsync(context, Config(demo));
        }

        [Fact]
        public async Task EmptyDatabase_GetsThreeCoursesWithStudentsInEveryModule()
        {
            await SeedAsync();

            await using var check = _db.NewContext();
            Assert.Equal(3, await check.Courses.CountAsync());
            Assert.Equal(16, await check.Modules.CountAsync());
            Assert.Equal(4, await check.Lecturers.CountAsync());
            Assert.Equal(30, await check.Students.CountAsync());
            Assert.True(await check.Modules.AllAsync(m => m.Students.Any() && m.CourseId != null));
            Assert.True(await check.Lecturers.AllAsync(l => l.Modules.Any()));
        }

        [Fact]
        public async Task EveryStudentEmail_IsACollegeAddress()
        {
            await SeedAsync();

            await using var check = _db.NewContext();
            var emails = await check.Students.Select(s => s.Email).ToListAsync();
            Assert.All(emails, e => Assert.EndsWith("@rcconnect.edu.za", e));
            Assert.Equal(emails.Count, emails.Distinct().Count());
        }

        [Fact]
        public async Task EveryDemoAccount_UsesTheDemoPassword()
        {
            await SeedAsync();

            await using var check = _db.NewContext();
            var hashes = await check.Students.Select(s => s.PasswordHash)
                .Concat(check.Lecturers.Select(l => l.PasswordHash))
                .Concat(check.Admins.Select(a => a.PasswordHash))
                .Distinct().ToListAsync();
            var hash = Assert.Single(hashes);
            Assert.True(BCrypt.Net.BCrypt.Verify("12345678", hash));
        }

        [Fact]
        public async Task Marks_AreReleasedForTheIceTaskOnly_AndEachHasHistory()
        {
            await SeedAsync();

            await using var check = _db.NewContext();
            var assessments = await check.Assessments.ToListAsync();
            Assert.All(assessments.Where(a => a.Type == "ICE"), a => Assert.True(a.MarksReleased));
            Assert.All(assessments.Where(a => a.Type != "ICE"), a => Assert.False(a.MarksReleased));

            var marks = await check.Marks.Include(m => m.Assessment).ToListAsync();
            Assert.Contains(marks, m => m.Assessment!.Type == "Quiz");
            Assert.DoesNotContain(marks, m => m.Assessment!.Type == "POE");
            Assert.All(marks, m => Assert.InRange(m.Score, 0, m.MaxScore));

            var history = await check.MarkChanges.Select(c => c.MarkId).ToListAsync();
            Assert.Equal(marks.Select(m => m.MarkId).Order(), history.Order());
        }

        [Fact]
        public async Task SomeStudents_AreAtRisk_ButMostAreNot()
        {
            await SeedAsync();

            await using var check = _db.NewContext();
            var averages = (await check.Marks.ToListAsync())
                .GroupBy(m => new { m.StudentId, m.ModuleId })
                .Select(g => g.Average(m => m.Percentage))
                .ToList();
            var failing = averages.Count(a => a < 50);
            Assert.InRange(failing, 1, averages.Count / 3);

            var sessions = await check.AttendanceSessions.CountAsync();
            var records = await check.AttendanceRecords.CountAsync();
            var enrolments = await check.Modules.SumAsync(m => m.Students.Count);
            Assert.InRange(records, enrolments * 6 / 2, enrolments * 6 - 1);
            Assert.Equal(16 * 6, sessions);
        }

        [Fact]
        public async Task SessionCodes_AreUniqueAndUseTheAttendanceAlphabet()
        {
            var codes = Enumerable.Range(0, 200).Select(DemoSeeder.SessionCode).ToList();

            Assert.Equal(codes.Count, codes.Distinct().Count());
            Assert.All(codes, c => Assert.Matches("^D[A-HJ-NP-Z2-9]{5}$", c));

            await SeedAsync();
            await using var check = _db.NewContext();
            Assert.True(await check.AttendanceSessions.AllAsync(s => s.Code.StartsWith("D") && s.IsClosed));
        }

        [Fact]
        public async Task TicketsAndAnnouncements_BelongToTheRightPeople()
        {
            await SeedAsync();

            await using var check = _db.NewContext();
            var tickets = await check.Tickets.Include(t => t.Student).ThenInclude(s => s!.Modules).ToListAsync();
            Assert.NotEmpty(tickets);
            Assert.All(tickets, t => Assert.Contains(t.Student!.Modules, m => m.ModuleId == t.ModuleId));
            Assert.Contains(tickets, t => t.Status == "Resolved" && t.DateResolved != null);

            Assert.Equal(3, await check.Announcements.CountAsync());
            Assert.True(await check.Notifications.AnyAsync(n => n.Role == Roles.Student && !n.IsRead));
        }

        [Fact]
        public async Task RunningAgain_AddsNothing()
        {
            await SeedAsync();
            await SeedAsync();

            await using var check = _db.NewContext();
            Assert.Equal(30, await check.Students.CountAsync());
            Assert.Equal(16, await check.Modules.CountAsync());
            Assert.Equal(16 * 3, await check.Assessments.CountAsync());
        }

        [Fact]
        public async Task WithoutTheFlag_OnlyTheNormalSeedRuns()
        {
            await SeedAsync(demo: false);

            await using var check = _db.NewContext();
            Assert.Equal(0, await check.Students.CountAsync());
            Assert.Equal(1, await check.Courses.CountAsync());
        }

        [Fact]
        public async Task ADatabaseWithStudents_IsLeftAlone()
        {
            await using (var setup = _db.NewContext())
            {
                setup.Students.Add(new Student { FullName = "Real Student", Email = "real@rcconnect.edu.za", PasswordHash = "x" });
                await setup.SaveChangesAsync();
            }

            await using (var context = _db.NewContext())
            {
                await DemoSeeder.SeedAsync(context, Config());
            }

            await using var check = _db.NewContext();
            Assert.Equal(1, await check.Students.CountAsync());
            Assert.Equal(0, await check.Lecturers.CountAsync());
        }

        [Fact]
        public void Ability_StaysBetween30And96()
        {
            var values = from s in Enumerable.Range(1, 60) from m in Enumerable.Range(1, 30) select DemoSeeder.Ability(s, m);

            Assert.All(values, v => Assert.InRange(v, 30m, 96m));
        }

        public void Dispose() => _db.Dispose();
    }
}
