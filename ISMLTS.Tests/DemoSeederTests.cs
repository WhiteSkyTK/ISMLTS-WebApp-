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
        public async Task OnALiveDatabase_RealAccountsAreKept_AndTakenEmailsSkipped()
        {
            int realLecturerId;
            await using (var setup = _db.NewContext())
            {
                var realLecturer = new Lecturer { FullName = "Real Lecturer", Email = "real.lecturer@rosebank.iie.ac.za", PasswordHash = "real-hash" };
                setup.Lecturers.Add(realLecturer);
                setup.Admins.Add(new Admin { Username = "realadmin", PasswordHash = "real-hash" });
                setup.Students.Add(new Student { FullName = "Real Student", Email = "real@rcconnect.edu.za", PasswordHash = "real-hash" });
                // Someone already owns the first demo student number
                setup.Students.Add(new Student { FullName = "Owner Of St1", Email = "st10000001@rcconnect.edu.za", PasswordHash = "real-hash" });
                await setup.SaveChangesAsync();
                realLecturerId = realLecturer.LecturerId;
            }

            await SeedAsync();

            await using var check = _db.NewContext();
            Assert.Equal(31, await check.Students.CountAsync());
            Assert.Equal("Owner Of St1", (await check.Students.SingleAsync(s => s.Email == "st10000001@rcconnect.edu.za")).FullName);
            Assert.True(await check.Students.Where(s => s.Email == "real@rcconnect.edu.za").Select(s => s.PasswordHash == "real-hash").SingleAsync());
            Assert.Equal(1, await check.Admins.CountAsync());
            // The demo teaching team is the real first lecturer plus the three demo lecturers
            Assert.Equal(4, await check.Lecturers.CountAsync());
            Assert.True(await check.Modules.AnyAsync(m => m.LecturerId == realLecturerId));
        }

        [Fact]
        public async Task AnAdminsChoiceOfLecturer_IsKept()
        {
            await using (var setup = _db.NewContext())
            {
                var hash = BCrypt.Net.BCrypt.HashPassword("x", workFactor: 4);
                var first = new Lecturer { FullName = "First", Email = "first@rosebank.iie.ac.za", PasswordHash = hash };
                var chosen = new Lecturer { FullName = "Chosen", Email = "chosen@rosebank.iie.ac.za", PasswordHash = hash };
                setup.Lecturers.AddRange(first, chosen);
                setup.Modules.Add(new Module { Code = "APDS7311", Name = "Security", Term = "Term1", Lecturer = chosen });
                await setup.SaveChangesAsync();
            }

            await SeedAsync();

            await using var check = _db.NewContext();
            var apds = await check.Modules.Include(m => m.Lecturer).SingleAsync(m => m.Code == "APDS7311");
            Assert.Equal("Chosen", apds.Lecturer!.FullName);
        }

        [Fact]
        public async Task ExistingAttendanceCodes_AreNotReused()
        {
            await using (var setup = _db.NewContext())
            {
                var lecturer = new Lecturer { FullName = "First", Email = "first@rosebank.iie.ac.za", PasswordHash = "x" };
                var module = new Module { Code = "REAL1234", Name = "Real", Lecturer = lecturer };
                setup.AttendanceSessions.Add(new AttendanceSession { Module = module, Code = DemoSeeder.SessionCode(1), StartedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow });
                await setup.SaveChangesAsync();
            }

            await SeedAsync();

            await using var check = _db.NewContext();
            var codes = await check.AttendanceSessions.Select(s => s.Code).ToListAsync();
            Assert.Equal(codes.Count, codes.Distinct().Count());
        }

        [Theory]
        [InlineData("1234567")]
        [InlineData("")]
        public void AShortDemoPassword_SwitchesTheSeedOff(string password)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:DemoData"] = "true",
                ["Seed:DemoPassword"] = password
            }).Build();

            Assert.False(DemoSeeder.IsEnabled(config));
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
