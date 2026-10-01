using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ISMLTS_WebApp_.Models;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    // Own factory: these tests add students, lecturers and a course
    public class ImportTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public ImportTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private HttpClient Admin() => _factory.ClientFor("Admin", _factory.Data.AdminId);

        private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string kind, string csv)
        {
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, $"/Import?kind={kind}");
            using var form = new MultipartFormDataContent
            {
                { new StringContent(token), "__RequestVerificationToken" },
                { new StringContent(kind), "kind" },
                { new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "people.csv" }
            };
            return await client.PostAsync("/Import", form);
        }

        private static async Task<HttpResponseMessage> ConfirmAsync(HttpClient client, string kind, string csv, params (string Key, string Value)[] extra)
        {
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, $"/Import?kind={kind}");
            var fields = new List<KeyValuePair<string, string>>
            {
                new("__RequestVerificationToken", token), new("kind", kind), new("csv", csv)
            };
            fields.AddRange(extra.Select(e => new KeyValuePair<string, string>(e.Key, e.Value)));
            return await client.PostAsync("/Import/Confirm", new FormUrlEncodedContent(fields));
        }

        [Fact]
        public async Task Preview_ShowsProblems_AndSavesNothing()
        {
            var response = await UploadAsync(Admin(), "students", "name,email\nNew Person,new.person@rcconnect.edu.za\nDuplicate,s@rcconnect.edu.za\n");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("Add 1 student", html);
            Assert.Contains("That email already belongs to a student or lecturer.", html);
            Assert.False(await _factory.WithDbAsync(db => db.Students.AnyAsync(s => s.Email == "new.person@rcconnect.edu.za")));
        }

        [Fact]
        public async Task Confirm_AddsStudentsWithTemporaryPasswords_AndEnrolsThem()
        {
            var data = _factory.Data;
            var courseId = await _factory.WithDbAsync(async db =>
            {
                var course = new Course { Code = "IMPT0101", Name = "Import Course" };
                db.Courses.Add(course);
                await db.SaveChangesAsync();
                await db.Modules.Where(m => m.ModuleId == data.ModuleAId).ExecuteUpdateAsync(m => m.SetProperty(x => x.CourseId, course.CourseId));
                return course.CourseId;
            });

            var response = await ConfirmAsync(Admin(), "students", "name,email,programme\nImported One,imported.one@rcconnect.edu.za,import course\n",
                ("courseId", courseId.ToString(CultureInfo.InvariantCulture)), ("term", "All"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("enrolled in IMPT0101", html);
            var temporary = Regex.Match(html, "<code class=\"temp-password\">([a-z0-9-]+)</code>", RegexOptions.None, TimeSpan.FromSeconds(1)).Groups[1].Value;
            var student = await _factory.WithDbAsync(db => db.Students.Include(s => s.Modules).SingleAsync(s => s.Email == "imported.one@rcconnect.edu.za"));
            Assert.True(BCrypt.Net.BCrypt.Verify(temporary, student.PasswordHash));
            Assert.True(student.MustChangePassword);
            Assert.Equal("Import Course", student.Programme);
            Assert.Contains(student.Modules, m => m.ModuleId == data.ModuleAId);
            Assert.Contains("data:text/csv;charset=utf-8;base64,", html);
        }

        [Fact]
        public async Task Confirm_ChecksTheFileAgain()
        {
            var response = await ConfirmAsync(Admin(), "lecturers", $"name,email\nCopy,{_factory.Data.StudentEmail}\n");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(1, await _factory.WithDbAsync(db => db.Students.CountAsync(s => s.Email == _factory.Data.StudentEmail)));
            Assert.False(await _factory.WithDbAsync(db => db.Lecturers.AnyAsync(l => l.Email == _factory.Data.StudentEmail)));
        }

        [Fact]
        public async Task Lecturers_CanBeImportedToo()
        {
            var response = await ConfirmAsync(Admin(), "lecturers", "name,email\nImported Lecturer,imported.lecturer@rosebank.iie.ac.za\n");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(await _factory.WithDbAsync(db => db.Lecturers.AnyAsync(l => l.Email == "imported.lecturer@rosebank.iie.ac.za" && l.MustChangePassword)));
        }

        [Fact]
        public async Task Template_IsACsv()
        {
            var response = await Admin().GetAsync("/Import/Template?kind=students");

            Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains("name,email,programme", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Lecturers_CannotImport()
        {
            var response = await _factory.ClientFor("Lecturer", _factory.Data.LecturerAId).GetAsync("/Import");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/AccessDenied", response.Headers.Location?.PathAndQuery);
        }
    }
}
