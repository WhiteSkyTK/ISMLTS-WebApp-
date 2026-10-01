using System.Net;
using ISMLTS_WebApp_.Models;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    public class ValidationTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public ValidationTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task CreatingAStudentWithATakenEmail_ShowsAMessage()
        {
            var response = await PostNewStudentAsync(_factory.Data.StudentEmail, "LongEnough1");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("That email already belongs to another student or lecturer.", await response.Content.ReadAsStringAsync());
            Assert.Equal(1, await CountStudentsWithEmailAsync(_factory.Data.StudentEmail));
        }

        [Fact]
        public async Task CreatingAStudentWithALecturersEmail_ShowsAMessage()
        {
            await _factory.WithDbAsync(async db =>
            {
                db.Lecturers.Add(new Lecturer { FullName = "Dual Role", Email = "dual@rcconnect.edu.za", PasswordHash = "x" });
                return await db.SaveChangesAsync();
            });

            var response = await PostNewStudentAsync("dual@rcconnect.edu.za", "LongEnough1");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("That email already belongs to another student or lecturer.", await response.Content.ReadAsStringAsync());
            Assert.Equal(0, await CountStudentsWithEmailAsync("dual@rcconnect.edu.za"));
        }

        [Theory]
        [InlineData("someone@gmail.com")]
        [InlineData("a@lecturers.test")]
        [InlineData("st10001@rcconnect.edu.za.evil.com")]
        public async Task CreatingAStudentOutsideTheCollegeDomain_ShowsAMessage(string email)
        {
            var response = await PostNewStudentAsync(email, "LongEnough1");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Student emails must end in @rcconnect.edu.za.", await response.Content.ReadAsStringAsync());
            Assert.Equal(0, await CountStudentsWithEmailAsync(email));
        }

        [Fact]
        public async Task CreatingAStudentWithAShortPassword_ShowsAMessage()
        {
            var response = await PostNewStudentAsync("short@rcconnect.edu.za", "Short1");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Password must be at least 8 characters.", await response.Content.ReadAsStringAsync());
            Assert.Equal(0, await CountStudentsWithEmailAsync("short@rcconnect.edu.za"));
        }

        [Fact]
        public async Task CreatingAValidStudent_Saves()
        {
            var response = await PostNewStudentAsync("new@rcconnect.edu.za", "LongEnough1");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(1, await CountStudentsWithEmailAsync("new@rcconnect.edu.za"));
        }

        [Fact]
        public async Task CreatingAModuleWithATakenCode_ShowsAMessage()
        {
            var client = _factory.ClientFor("Admin", _factory.Data.AdminId);
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, "/Modules/Create");

            var response = await client.PostAsync("/Modules/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Code"] = _factory.Data.ModuleACode,
                ["Name"] = "Duplicate",
                ["LecturerId"] = _factory.Data.LecturerAId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Term"] = "Term1"
            }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Another module already uses that code.", await response.Content.ReadAsStringAsync());
        }

        private async Task<HttpResponseMessage> PostNewStudentAsync(string email, string password)
        {
            var client = _factory.ClientFor("Admin", _factory.Data.AdminId);
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, "/Students/Create");

            return await client.PostAsync("/Students/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["FullName"] = "New Student",
                ["Email"] = email,
                ["password"] = password
            }));
        }

        private Task<int> CountStudentsWithEmailAsync(string email) =>
            _factory.WithDbAsync(db => db.Students.CountAsync(s => s.Email == email));
    }
}
