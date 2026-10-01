using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ISMLTS_WebApp_.Services;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    // Own factory: these tests reset passwords and switch two-factor off
    public class UserSecurityTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public UserSecurityTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string formUrl, string url, params (string Key, string Value)[] fields)
        {
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, formUrl);
            return await client.PostAsync(url, new FormUrlEncodedContent(
                fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value))
                    .Prepend(new KeyValuePair<string, string>("__RequestVerificationToken", token))));
        }

        private HttpClient Admin() => _factory.ClientFor("Admin", _factory.Data.AdminId);

        [Fact]
        public async Task DetailsPages_ShowTheSecurityPanel()
        {
            var html = await Admin().GetStringAsync($"/Lecturers/Details/{Id(_factory.Data.LecturerAId)}");

            Assert.Contains("Sign-in and security", html);
            Assert.Contains("Reset password", html);
        }

        [Fact]
        public async Task ResetPassword_ShowsATemporaryPasswordOnce_ThatMustBeChanged()
        {
            var data = _factory.Data;
            var details = $"/Lecturers/Details/{Id(data.LecturerAId)}";

            var response = await PostAsync(Admin(), details, $"/UserSecurity/ResetPassword?role=Lecturer&id={Id(data.LecturerAId)}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            var temporary = Regex.Match(html, "<code class=\"temp-password\">([a-z0-9-]+)</code>", RegexOptions.None, TimeSpan.FromSeconds(1)).Groups[1].Value;
            Assert.Matches("^[a-z2-9]{4}-[a-z2-9]{4}-[a-z2-9]{4}$", temporary);
            var lecturer = await _factory.WithDbAsync(db => db.Lecturers.SingleAsync(l => l.LecturerId == data.LecturerAId));
            Assert.True(BCrypt.Net.BCrypt.Verify(temporary, lecturer.PasswordHash));
            Assert.True(lecturer.MustChangePassword);
        }

        [Fact]
        public async Task TemporaryPasswordSessions_CanOnlyReachTheProfile_UntilItIsChanged()
        {
            var data = _factory.Data;
            await _factory.WithDbAsync(db => db.Students.Where(s => s.StudentId == data.OtherStudentId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.MustChangePassword, true)));
            var client = _factory.ClientFor("Student", data.OtherStudentId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.MustChangePasswordHeader, "1");

            var marks = await client.GetAsync("/Marks/MyMarks");
            Assert.Equal(HttpStatusCode.Redirect, marks.StatusCode);
            Assert.Equal("/Profile", marks.Headers.Location?.OriginalString);
            Assert.Contains("An admin reset your password.", await client.GetStringAsync("/Profile"));

            var changed = await PostAsync(client, "/Profile", "/Profile/ChangePassword",
                ("Password.CurrentPassword", data.Password), ("Password.NewPassword", "Fresh-Pass-11"), ("Password.ConfirmPassword", "Fresh-Pass-11"));
            Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
            Assert.False(await _factory.WithDbAsync(db => db.Students.Where(s => s.StudentId == data.OtherStudentId).Select(s => s.MustChangePassword).SingleAsync()));
        }

        [Fact]
        public async Task TurnOffTwoFactor_ClearsTheApp_ForSomeoneWhoLostTheirPhone()
        {
            var data = _factory.Data;
            await _factory.WithDbAsync(db => db.Students.Where(s => s.StudentId == data.StudentId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.TwoFactorSecret, TwoFactor.NewSecret()).SetProperty(x => x.TwoFactorEnabled, true)));
            var details = $"/Students/Details/{Id(data.StudentId)}";
            Assert.Contains("Turn off two-factor", await Admin().GetStringAsync(details));

            var response = await PostAsync(Admin(), details, $"/UserSecurity/TurnOffTwoFactor?role=Student&id={Id(data.StudentId)}");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(details, response.Headers.Location?.OriginalString);
            var student = await _factory.WithDbAsync(db => db.Students.SingleAsync(s => s.StudentId == data.StudentId));
            Assert.False(student.TwoFactorEnabled);
            Assert.Null(student.TwoFactorSecret);
        }

        [Fact]
        public async Task UnknownRolesOrIds_AreNotFound()
        {
            var response = await PostAsync(Admin(), "/Students", "/UserSecurity/ResetPassword?role=Janitor&id=1");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Lecturers_CannotResetPasswords()
        {
            var lecturer = _factory.ClientFor("Lecturer", _factory.Data.LecturerBId);

            var response = await PostAsync(lecturer, "/Profile", $"/UserSecurity/ResetPassword?role=Student&id={Id(_factory.Data.OtherStudentId)}");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/AccessDenied", response.Headers.Location?.PathAndQuery);
        }
    }
}
