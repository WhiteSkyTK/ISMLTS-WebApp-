using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using ISMLTS_WebApp_.Models;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    // Phase 9: the admin audit log and a student's own data download
    public class DataCareTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public DataCareTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);

        private HttpClient Admin() => _factory.ClientFor(Roles.Admin, _factory.Data.AdminId);

        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string formPage, string url, Dictionary<string, string>? fields = null)
        {
            fields ??= new();
            fields["__RequestVerificationToken"] = await IsmltsFactory.GetAntiforgeryTokenAsync(client, formPage);
            return await client.PostAsync(url, new FormUrlEncodedContent(fields));
        }

        [Fact]
        public async Task PasswordResets_AreAudited_WithoutThePassword()
        {
            var admin = Admin();
            var reset = await PostAsync(admin, $"/Lecturers/Details/{Id(_factory.Data.LecturerBId)}", $"/UserSecurity/ResetPassword?role=Lecturer&id={Id(_factory.Data.LecturerBId)}");
            var html = await reset.Content.ReadAsStringAsync();
            var temporary = Regex.Match(html, "<code[^>]*>([^<]+)</code>", RegexOptions.None, TimeSpan.FromSeconds(1)).Groups[1].Value;

            var entry = await _factory.WithDbAsync(db => db.AuditEntries.Where(e => e.Action == AuditActions.PasswordReset).OrderByDescending(e => e.AuditEntryId).FirstAsync());
            Assert.Contains("b@lecturers.test", entry.Target);
            Assert.Equal((Roles.Admin, _factory.Data.AdminId), (entry.ActorRole, entry.ActorId));
            Assert.False(string.IsNullOrEmpty(temporary));
            Assert.DoesNotContain(temporary, $"{entry.Target} {entry.Details}");

            var page = await admin.GetStringAsync("/Audit?action=" + Uri.EscapeDataString(AuditActions.PasswordReset));
            Assert.Contains("b@lecturers.test", page);
        }

        [Fact]
        public async Task EnrolmentChanges_AreAudited_AndOnlyAdminsSeeTheLog()
        {
            var admin = Admin();
            var moduleId = Id(_factory.Data.ModuleAId);
            var response = await PostAsync(admin, $"/Modules/Enrol/{moduleId}", $"/Modules/Enrol/{moduleId}", new()
            {
                ["selectedStudentIds"] = Id(_factory.Data.StudentId)
            });

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.True(await _factory.WithDbAsync(db => db.AuditEntries.AnyAsync(e => e.Action == AuditActions.EnrolmentChanged && e.Target == "Module " + _factory.Data.ModuleACode)));
            foreach (var other in new[] { _factory.ClientFor(Roles.Lecturer, _factory.Data.LecturerAId), _factory.ClientFor(Roles.Student, _factory.Data.StudentId) })
            {
                var denied = await other.GetAsync("/Audit");
                Assert.StartsWith("/Account/AccessDenied", denied.Headers.Location?.PathAndQuery);
            }
        }

        [Fact]
        public async Task Students_DownloadTheirOwnData_WithOnlyReleasedMarks()
        {
            await _factory.WithDbAsync(async db =>
            {
                db.Marks.AddRange(
                    new Mark { StudentId = _factory.Data.StudentId, ModuleId = _factory.Data.ModuleAId, AssessmentId = _factory.Data.AssessmentAId, AssessmentName = "POE A", Score = 31, MaxScore = 50 },
                    new Mark { StudentId = _factory.Data.StudentId, ModuleId = _factory.Data.ModuleAId, AssessmentName = "Class test", Score = 18, MaxScore = 20 });
                return await db.SaveChangesAsync();
            });

            var response = await _factory.ClientFor(Roles.Student, _factory.Data.StudentId).GetAsync("/MyData/Download");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
            using var zip = new ZipArchive(await response.Content.ReadAsStreamAsync());
            Assert.Equal(["attendance.csv", "calendar-notes.csv", "marks.csv", "profile.csv", "submissions.csv", "tickets.csv"], zip.Entries.Select(e => e.Name).Order().ToArray());
            string Read(string name) => new StreamReader(zip.GetEntry(name)!.Open()).ReadToEnd();
            Assert.Contains("s@rcconnect.edu.za", Read("profile.csv"));
            var marks = Read("marks.csv");
            Assert.Contains("Class test", marks);
            Assert.DoesNotContain("POE A", marks); // not released yet
            Assert.DoesNotContain("t@rcconnect.edu.za", string.Join("", zip.Entries.Select(e => Read(e.Name))));

            var lecturer = await _factory.ClientFor(Roles.Lecturer, _factory.Data.LecturerAId).GetAsync("/MyData/Download");
            Assert.StartsWith("/Account/AccessDenied", lecturer.Headers.Location?.PathAndQuery);
        }
    }
}
