using System.Globalization;
using System.Text.RegularExpressions;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ISMLTS.Tests.Integration
{
    public record SeedData(
        int AdminId,
        int LecturerAId, int LecturerBId,
        int StudentId,
        int ModuleAId, int ModuleBId,
        string ModuleACode, string ModuleBCode,
        int AssessmentAId, int AssessmentBId,
        int MarkBId, int TicketBId,
        string StudentEmail, string Password);

    // Runs the real app in the "Testing" environment against a fresh SQLite in-memory database
    public class IsmltsFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private SeedData? _data;

        public SeedData Data
        {
            get
            {
                _ = Server; // builds the host, which seeds the database
                return _data!;
            }
        }

        public HttpClient ClientFor(string? role = null, int? userId = null)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
            if (role != null && userId != null)
            {
                client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
                client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.Value.ToString(CultureInfo.InvariantCulture));
            }
            return client;
        }

        public static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string formUrl)
        {
            var html = await client.GetStringAsync(formUrl);
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"", RegexOptions.None, TimeSpan.FromSeconds(1));
            Assert.True(match.Success, $"No antiforgery token on {formUrl}");
            return match.Groups[1].Value;
        }

        public async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> query)
        {
            using var scope = Services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                _connection.Open();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
                services.AddAuthentication(options => options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
            _data = Seed(db);
            return host;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _connection.Dispose();
        }

        private static SeedData Seed(ApplicationDbContext db)
        {
            const string password = "Password1!";
            var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4);

            var admin = new Admin { Username = "admin", PasswordHash = hash };
            var lecturerA = new Lecturer { FullName = "Lecturer A", Email = "a@lecturers.test", PasswordHash = hash };
            var lecturerB = new Lecturer { FullName = "Lecturer B", Email = "b@lecturers.test", PasswordHash = hash };
            var moduleA = new Module { Code = "AAAA1111", Name = "Module A", Lecturer = lecturerA };
            var moduleB = new Module { Code = "BBBB2222", Name = "Module B", Lecturer = lecturerB };
            var student = new Student { FullName = "Student A", Email = "s@students.test", PasswordHash = hash, Modules = { moduleA } };
            var otherStudent = new Student { FullName = "Student B", Email = "t@students.test", PasswordHash = hash, Modules = { moduleB } };
            var assessmentA = new Assessment { Module = moduleA, Name = "POE A", DueDate = DateTime.Today.AddDays(7) };
            var assessmentB = new Assessment { Module = moduleB, Name = "POE B", DueDate = DateTime.Today.AddDays(7) };
            var markB = new Mark { Student = otherStudent, Module = moduleB, AssessmentName = "Quiz", Score = 40, MaxScore = 50 };
            var ticketB = new Ticket { Student = otherStudent, Module = moduleB, Subject = "Help", Description = "Question about Module B" };

            db.AddRange(admin, lecturerA, lecturerB, moduleA, moduleB, student, otherStudent, assessmentA, assessmentB, markB, ticketB);
            db.SaveChanges();

            return new SeedData(
                admin.AdminId,
                lecturerA.LecturerId, lecturerB.LecturerId,
                student.StudentId,
                moduleA.ModuleId, moduleB.ModuleId,
                moduleA.Code, moduleB.Code,
                assessmentA.AssessmentId, assessmentB.AssessmentId,
                markB.MarkId, ticketB.TicketId,
                student.Email, password);
        }
    }
}
