using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Models.Api;
using ISMLTS_WebApp_.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OtpNet;

namespace ISMLTS.Tests.Integration
{
    // The real app with a high log-in limit, so these tests can log in as often as they need
    public class ApiFactory : IsmltsFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("RateLimiting:LoginAttemptsPerMinute", "1000");
        }
    }

    public class ApiTests : IClassFixture<ApiFactory>
    {
        private readonly ApiFactory _factory;

        public ApiTests(ApiFactory factory)
        {
            _factory = factory;
        }

        private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);

        private Task<HttpResponseMessage> LoginAsync(string login, string password, string? code = null) =>
            _factory.ClientFor().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(login, password, code));

        private async Task<TokenResponse> TokensAsync(string? email = null)
        {
            var response = await LoginAsync(email ?? _factory.Data.StudentEmail, _factory.Data.Password);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        }

        private async Task<HttpClient> AppAsync(string? email = null)
        {
            var client = _factory.ClientFor();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await TokensAsync(email)).AccessToken);
            return client;
        }

        private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
        {
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
        }

        // A student of their own, so tests that change an account don't affect the others
        private Task<string> NewStudentAsync(Action<Student>? change = null) =>
            _factory.WithDbAsync(async db =>
            {
                var email = $"api{Guid.NewGuid():N}@rcconnect.edu.za"[..30] + "@rcconnect.edu.za";
                var student = new Student { FullName = "Api Student", Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(_factory.Data.Password, workFactor: 4) };
                change?.Invoke(student);
                db.Students.Add(student);
                await db.SaveChangesAsync();
                return email;
            });

        // ---------- Logging in ----------

        [Fact]
        public async Task Login_GivesAStudentTokens()
        {
            var tokens = await TokensAsync();

            Assert.False(string.IsNullOrEmpty(tokens.AccessToken));
            Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
            Assert.True(tokens.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(50));
            Assert.Equal(_factory.Data.StudentEmail, tokens.Student.Email);
        }

        [Fact]
        public async Task Login_WithAWrongPassword_IsAJsonProblem()
        {
            var response = await LoginAsync(_factory.Data.StudentEmail, "wrong-password");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("invalid_login", await ProblemCodeAsync(response));
        }

        [Theory]
        [InlineData("a@lecturers.test")]
        [InlineData("admin")]
        public async Task Login_IsForStudentsOnly(string login)
        {
            var response = await LoginAsync(login, _factory.Data.Password);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("students_only", await ProblemCodeAsync(response));
        }

        [Fact]
        public async Task Login_WithATemporaryPassword_SendsThemToTheWebsite()
        {
            var email = await NewStudentAsync(s => s.MustChangePassword = true);

            var response = await LoginAsync(email, _factory.Data.Password);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("password_change_required", await ProblemCodeAsync(response));
        }

        [Fact]
        public async Task Login_WithTwoFactor_NeedsTheCode()
        {
            var secret = TwoFactor.NewSecret();
            var email = await NewStudentAsync(s => { s.TwoFactorSecret = secret; s.TwoFactorEnabled = true; });

            var noCode = await LoginAsync(email, _factory.Data.Password);
            Assert.Equal("two_factor_required", await ProblemCodeAsync(noCode));

            var wrongCode = await LoginAsync(email, _factory.Data.Password, "000000" == CurrentCode(secret) ? "111111" : "000000");
            Assert.Equal("invalid_code", await ProblemCodeAsync(wrongCode));

            var withCode = await LoginAsync(email, _factory.Data.Password, CurrentCode(secret));
            Assert.Equal(HttpStatusCode.OK, withCode.StatusCode);
        }

        private static string CurrentCode(string secret) => new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(DateTime.UtcNow);

        // ---------- Who can call the API ----------

        [Fact]
        public async Task WithoutAToken_TheApiAnswers401InJson_NotALoginPage()
        {
            var response = await _factory.ClientFor().GetAsync("/api/v1/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("unauthorized", await ProblemCodeAsync(response));
        }

        [Fact]
        public async Task AWebsiteSession_DoesNotOpenTheApi()
        {
            var response = await _factory.ClientFor("Student", _factory.Data.StudentId).GetAsync("/api/v1/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AForgedToken_IsRejected()
        {
            var client = _factory.ClientFor();
            var forged = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = "ISMLTS",
                Audience = "ISMLTS.Android",
                Expires = DateTime.UtcNow.AddHours(1),
                Claims = new Dictionary<string, object> { ["sub"] = Id(_factory.Data.StudentId), ["role"] = "Student" },
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(new byte[64]), SecurityAlgorithms.HmacSha256)
            });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        }

        [Fact]
        public async Task AnExpiredToken_SaysSo()
        {
            var key = _factory.Services.GetRequiredService<ApiSigningKey>();
            var expired = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = "ISMLTS",
                Audience = "ISMLTS.Android",
                IssuedAt = DateTime.UtcNow.AddHours(-3),
                NotBefore = DateTime.UtcNow.AddHours(-3),
                Expires = DateTime.UtcNow.AddHours(-2),
                Claims = new Dictionary<string, object> { ["sub"] = Id(_factory.Data.StudentId), ["role"] = "Student" },
                SigningCredentials = new SigningCredentials(key.Key, SecurityAlgorithms.HmacSha256)
            });
            var client = _factory.ClientFor();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expired);

            var response = await client.GetAsync("/api/v1/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("token_expired", await ProblemCodeAsync(response));
        }

        [Fact]
        public async Task Swagger_IsOnlyInDevelopment()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await _factory.ClientFor().GetAsync("/swagger/v1/swagger.json")).StatusCode);
        }

        // ---------- Refresh and log out ----------

        [Fact]
        public async Task Refresh_GivesANewPair_AndEachRefreshTokenWorksOnce()
        {
            var first = await TokensAsync();
            var client = _factory.ClientFor();

            var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(first.RefreshToken));
            Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
            var second = (await refreshed.Content.ReadFromJsonAsync<TokenResponse>())!;
            Assert.NotEqual(first.RefreshToken, second.RefreshToken);

            // Using the old one again looks like a stolen token: it fails, and so does the newer one
            var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(first.RefreshToken));
            Assert.Equal("invalid_refresh_token", await ProblemCodeAsync(replay));
            var afterReplay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(second.RefreshToken));
            Assert.Equal(HttpStatusCode.Unauthorized, afterReplay.StatusCode);
        }

        [Fact]
        public async Task Logout_EndsTheRefreshToken()
        {
            var tokens = await TokensAsync();
            var client = _factory.ClientFor();

            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/logout", new RefreshRequest(tokens.RefreshToken))).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken))).StatusCode);
        }

        [Fact]
        public async Task APasswordReset_EndsEveryAppSession()
        {
            var email = await NewStudentAsync();
            var tokens = await TokensAsync(email);

            using (var scope = _factory.Services.CreateScope())
            {
                var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();
                var studentId = await _factory.WithDbAsync(db => db.Students.Where(s => s.Email == email).Select(s => s.StudentId).SingleAsync());
                await accounts.ResetToTemporaryPasswordAsync((await accounts.FindAsync(Roles.Student, studentId))!);
            }

            var refresh = await _factory.ClientFor().PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken));
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }

        // ---------- The student's data ----------

        [Fact]
        public async Task Me_AndModules_DescribeTheSignedInStudent()
        {
            var app = await AppAsync();

            var me = await app.GetFromJsonAsync<StudentDto>("/api/v1/me");
            var modules = await app.GetFromJsonAsync<List<ModuleDto>>("/api/v1/modules");

            Assert.Equal(_factory.Data.StudentId, me!.StudentId);
            var module = Assert.Single(modules!);
            Assert.Equal((_factory.Data.ModuleACode, "Lecturer A"), (module.Code, module.Lecturer));
        }

        [Fact]
        public async Task Marks_OnlyIncludeReleasedOnes()
        {
            var data = _factory.Data;
            await _factory.WithDbAsync(async db =>
            {
                db.Marks.Add(new Mark { StudentId = data.StudentId, ModuleId = data.ModuleAId, AssessmentId = data.AssessmentAId, AssessmentName = "POE A", Score = 61, MaxScore = 100 });
                db.Marks.Add(new Mark { StudentId = data.StudentId, ModuleId = data.ModuleAId, AssessmentName = "Class test", Score = 15, MaxScore = 20 });
                return await db.SaveChangesAsync();
            });

            var marks = await (await AppAsync()).GetFromJsonAsync<List<MarkListItemDto>>("/api/v1/marks");

            Assert.Contains(marks!, m => m.Name == "Class test" && m.Percentage == 75m);
            Assert.DoesNotContain(marks!, m => m.AssessmentId == data.AssessmentAId);
        }

        [Fact]
        public async Task Assessments_UsePlainDueDates_AndMachineStatuses()
        {
            var app = await AppAsync();

            var json = await app.GetStringAsync("/api/v1/assessments");

            Assert.Matches("\"dueDate\":\"\\d{4}-\\d{2}-\\d{2}\"", json);
            Assert.Matches("\"status\":\"(not_submitted|submitted|late)\"", json);
        }

        [Fact]
        public async Task Submit_ChecksTheLink_AndTheModule()
        {
            var data = _factory.Data;
            var app = await AppAsync();

            var badLink = await app.PutAsJsonAsync($"/api/v1/assessments/{Id(data.AssessmentAId)}/submission", new SubmissionRequest("javascript:alert(1)"));
            Assert.Equal("invalid_link", await ProblemCodeAsync(badLink));

            var otherModule = await app.PutAsJsonAsync($"/api/v1/assessments/{Id(data.AssessmentBId)}/submission", new SubmissionRequest("https://github.com/a/b"));
            Assert.Equal(HttpStatusCode.NotFound, otherModule.StatusCode);

            var saved = await app.PutAsJsonAsync($"/api/v1/assessments/{Id(data.AssessmentAId)}/submission", new SubmissionRequest("https://github.com/student-a/api"));
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            var assessment = (await saved.Content.ReadFromJsonAsync<AssessmentDto>())!;
            Assert.Equal(("https://github.com/student-a/api", true), (assessment.Link, assessment.SubmittedAt != null));
            Assert.NotEqual("not_submitted", assessment.Status);
        }

        [Fact]
        public async Task Tickets_GoToTheStudentsOwnModulesOnly()
        {
            var data = _factory.Data;
            var app = await AppAsync();

            var created = await app.PostAsJsonAsync("/api/v1/tickets", new TicketRequest(data.ModuleAId, "From the app", "Is the POE due on Friday?"));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Equal("open", (await created.Content.ReadFromJsonAsync<TicketDto>())!.Status);

            var otherModule = await app.PostAsJsonAsync("/api/v1/tickets", new TicketRequest(data.ModuleBId, "Sneaky", "Not my module"));
            Assert.Equal("invalid_module", await ProblemCodeAsync(otherModule));

            var mine = await app.GetFromJsonAsync<List<TicketDto>>("/api/v1/tickets");
            Assert.Contains(mine!, t => t.Subject == "From the app");
        }

        [Fact]
        public async Task Notifications_CanBeListedAndRead_ButNotSomeoneElses()
        {
            var data = _factory.Data;
            var (mine, theirs) = await _factory.WithDbAsync(async db =>
            {
                var a = new Notification { Role = Roles.Student, UserId = data.StudentId, Title = "For the app" };
                var b = new Notification { Role = Roles.Student, UserId = data.OtherStudentId, Title = "Someone else's" };
                db.Notifications.AddRange(a, b);
                await db.SaveChangesAsync();
                return (a.NotificationId, b.NotificationId);
            });
            var app = await AppAsync();

            var page = await app.GetFromJsonAsync<NotificationPageDto>("/api/v1/notifications?unreadOnly=true");
            Assert.Contains(page!.Items, n => n.NotificationId == mine);
            Assert.DoesNotContain(page.Items, n => n.NotificationId == theirs);

            Assert.Equal(HttpStatusCode.NoContent, (await app.PostAsync($"/api/v1/notifications/{Id(mine)}/read", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await app.PostAsync($"/api/v1/notifications/{Id(theirs)}/read", null)).StatusCode);
            Assert.True(await _factory.WithDbAsync(db => db.Notifications.Where(n => n.NotificationId == mine).Select(n => n.IsRead).SingleAsync()));
        }

        [Fact]
        public async Task Scan_MarksThemPresentOnce()
        {
            var data = _factory.Data;
            await _factory.WithDbAsync(async db =>
            {
                db.AttendanceSessions.Add(new AttendanceSession
                {
                    ModuleId = data.ModuleAId, Code = "APP001", StartedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    Latitude = -26.1455, Longitude = 28.0436
                });
                return await db.SaveChangesAsync();
            });
            var app = await AppAsync();

            var unknown = await app.PostAsJsonAsync("/api/v1/attendance/scan", new ScanRequest("ZZZ999", null, null, null));
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
            Assert.Equal(ScanCodes.UnknownCode, await ProblemCodeAsync(unknown));

            var present = await app.PostAsJsonAsync("/api/v1/attendance/scan", new ScanRequest("app001", -26.1456, 28.0438, 12));
            Assert.Equal(HttpStatusCode.OK, present.StatusCode);
            Assert.Equal(data.ModuleACode, (await present.Content.ReadFromJsonAsync<ScanResponse>())!.ModuleCode);

            var again = await app.PostAsJsonAsync("/api/v1/attendance/scan", new ScanRequest("APP001", -26.1456, 28.0438, 12));
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

            var attendance = await app.GetFromJsonAsync<List<AttendanceDto>>("/api/v1/attendance");
            Assert.Contains(attendance!, a => a.ModuleCode == data.ModuleACode && a.Attended >= 1);
        }

        [Fact]
        public async Task Announcements_ShowCollegeWidePosts()
        {
            await _factory.WithDbAsync(async db =>
            {
                db.Announcements.Add(new Announcement { Title = "Exam timetable", Body = "Out now", Audience = AnnouncementAudiences.Everyone, AuthorRole = Roles.Admin, AuthorId = 1, AuthorName = "admin" });
                return await db.SaveChangesAsync();
            });

            var list = await (await AppAsync()).GetFromJsonAsync<List<AnnouncementDto>>("/api/v1/announcements");

            Assert.Contains(list!, a => a.Title == "Exam timetable");
        }
    }

    // Default factory (5 log-ins a minute): the app gets a JSON 429, not the website's page
    public class ApiRateLimitTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public ApiRateLimitTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task TooManyLogins_GetAJson429()
        {
            HttpResponseMessage? last = null;
            for (var i = 0; i < 7; i++)
            {
                last = await _factory.ClientFor().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("nobody@rcconnect.edu.za", "wrong", null));
            }

            Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
            Assert.Equal("application/problem+json", last.Content.Headers.ContentType?.MediaType);
            Assert.Contains("too_many_attempts", await last.Content.ReadAsStringAsync());
        }
    }
}
