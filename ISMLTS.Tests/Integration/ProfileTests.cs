using System.Net;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    // Own factory: these tests change passwords (and the password form shares the login rate limit)
    public class ProfileTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public ProfileTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private static async Task<HttpResponseMessage> ChangePasswordAsync(HttpClient client, string current, string next, string confirm)
        {
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, "/Profile");
            return await client.PostAsync("/Profile/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Password.CurrentPassword"] = current,
                ["Password.NewPassword"] = next,
                ["Password.ConfirmPassword"] = confirm
            }));
        }

        [Theory]
        [InlineData("Admin", "admin")]
        [InlineData("Lecturer", "Lecturer A")]
        [InlineData("Student", "Student A")]
        public async Task EveryRole_SeesTheirOwnProfile(string role, string name)
        {
            var data = _factory.Data;
            var id = role switch { "Admin" => data.AdminId, "Lecturer" => data.LecturerAId, _ => data.StudentId };

            var html = await _factory.ClientFor(role, id).GetStringAsync("/Profile");

            Assert.Contains(name, html);
            Assert.Contains("Change password", html);
        }

        [Fact]
        public async Task StudentProfile_ListsTheirModules()
        {
            var html = await _factory.ClientFor("Student", _factory.Data.StudentId).GetStringAsync("/Profile");

            Assert.Contains($"{_factory.Data.ModuleACode} - Module A", html);
            Assert.DoesNotContain(_factory.Data.ModuleBCode, html);
        }

        [Fact]
        public async Task WrongCurrentPassword_ChangesNothing()
        {
            var data = _factory.Data;
            var client = _factory.ClientFor("Student", data.OtherStudentId);

            var response = await ChangePasswordAsync(client, "not-my-password", "BrandNew123", "BrandNew123");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Your current password is not right.", await response.Content.ReadAsStringAsync());
            var hash = await _factory.WithDbAsync(db => db.Students.Where(s => s.StudentId == data.OtherStudentId).Select(s => s.PasswordHash).SingleAsync());
            Assert.True(BCrypt.Net.BCrypt.Verify(data.Password, hash));
        }

        [Fact]
        public async Task ShortOrMismatchedNewPasswords_AreRejected()
        {
            var client = _factory.ClientFor("Lecturer", _factory.Data.LecturerAId);

            var tooShort = await ChangePasswordAsync(client, _factory.Data.Password, "short", "short");
            Assert.Contains("Password must be at least 8 characters.", await tooShort.Content.ReadAsStringAsync());

            var mismatch = await ChangePasswordAsync(client, _factory.Data.Password, "LongEnough1", "LongEnough2");
            Assert.Contains("The two new passwords don&#x27;t match.", await mismatch.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task RightCurrentPassword_SavesTheNewOne()
        {
            var data = _factory.Data;
            var client = _factory.ClientFor("Lecturer", data.LecturerBId);

            var response = await ChangePasswordAsync(client, data.Password, "Changed-Pass-9", "Changed-Pass-9");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var hash = await _factory.WithDbAsync(db => db.Lecturers.Where(l => l.LecturerId == data.LecturerBId).Select(l => l.PasswordHash).SingleAsync());
            Assert.True(BCrypt.Net.BCrypt.Verify("Changed-Pass-9", hash));
        }

        [Fact]
        public async Task SignedOutVisitors_GoToLogin()
        {
            var response = await _factory.ClientFor().GetAsync("/Profile");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/Login", response.Headers.Location?.PathAndQuery);
        }
    }
}
