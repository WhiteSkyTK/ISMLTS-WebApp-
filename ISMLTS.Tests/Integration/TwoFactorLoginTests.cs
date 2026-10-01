using System.Net;
using System.Text.RegularExpressions;
using ISMLTS_WebApp_.Services;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace ISMLTS.Tests.Integration
{
    // Own factory: logs in for real (cookies, not test headers) and switches two-factor on
    public class TwoFactorLoginTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public TwoFactorLoginTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private static string CurrentCode(string secret) => new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(DateTime.UtcNow);

        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string formUrl, string url, params (string Key, string Value)[] fields)
        {
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, formUrl);
            return await client.PostAsync(url, new FormUrlEncodedContent(
                fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value))
                    .Prepend(new KeyValuePair<string, string>("__RequestVerificationToken", token))));
        }

        private static bool SetsSessionCookie(HttpResponseMessage response) =>
            response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith(".AspNetCore.Cookies=", StringComparison.Ordinal));

        [Fact]
        public async Task LecturerWithAnApp_NeedsTheCode_BeforeGettingASession()
        {
            var data = _factory.Data;
            var secret = TwoFactor.NewSecret();
            await _factory.WithDbAsync(db => db.Lecturers.Where(l => l.LecturerId == data.LecturerAId)
                .ExecuteUpdateAsync(l => l.SetProperty(x => x.TwoFactorSecret, secret).SetProperty(x => x.TwoFactorEnabled, true)));
            var client = _factory.ClientFor();

            var login = await PostAsync(client, "/Account/Login", "/Account/Login?returnUrl=%2FMarks",
                ("EmailOrUsername", "a@lecturers.test"), ("Password", data.Password));
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            Assert.Equal("/Account/TwoFactor?returnUrl=%2FMarks", login.Headers.Location?.OriginalString);
            Assert.False(SetsSessionCookie(login));

            var wrong = await PostAsync(client, "/Account/TwoFactor", "/Account/TwoFactor?returnUrl=%2FMarks", ("code", "abc"));
            Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
            Assert.Contains("That code didn&#x27;t work.", await wrong.Content.ReadAsStringAsync());
            Assert.False(SetsSessionCookie(wrong));

            var right = await PostAsync(client, "/Account/TwoFactor", "/Account/TwoFactor?returnUrl=%2FMarks", ("code", CurrentCode(secret)));
            Assert.Equal(HttpStatusCode.Redirect, right.StatusCode);
            Assert.Equal("/Marks", right.Headers.Location?.OriginalString);
            Assert.True(SetsSessionCookie(right));
        }

        [Fact]
        public async Task CodePage_WithoutAPasswordFirst_GoesBackToLogin()
        {
            var response = await _factory.ClientFor().GetAsync("/Account/TwoFactor");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/Login", response.Headers.Location?.OriginalString);
        }

        [Fact]
        public async Task StudentWithoutAnApp_LogsInWithJustThePassword()
        {
            var client = _factory.ClientFor();

            var login = await PostAsync(client, "/Account/Login", "/Account/Login",
                ("EmailOrUsername", _factory.Data.StudentEmail), ("Password", _factory.Data.Password));

            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            Assert.Equal("/", login.Headers.Location?.OriginalString);
            Assert.True(SetsSessionCookie(login));
        }
    }

    // Own factory: the seeded admin sets up two-factor while logging in
    public class AdminTwoFactorTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public AdminTwoFactorTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string formUrl, string url, params (string Key, string Value)[] fields)
        {
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, formUrl);
            return await client.PostAsync(url, new FormUrlEncodedContent(
                fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value))
                    .Prepend(new KeyValuePair<string, string>("__RequestVerificationToken", token))));
        }

        [Fact]
        public async Task AdminWithoutAnApp_SetsOneUpDuringLogin()
        {
            // Another test in this class switches it on; start from off whatever the order
            await _factory.WithDbAsync(db => db.Admins.Where(a => a.AdminId == _factory.Data.AdminId)
                .ExecuteUpdateAsync(a => a.SetProperty(x => x.TwoFactorEnabled, false).SetProperty(x => x.TwoFactorSecret, (string?)null)));
            var client = _factory.ClientFor();

            var login = await PostAsync(client, "/Account/Login", "/Account/Login", ("EmailOrUsername", "admin"), ("Password", _factory.Data.Password));
            Assert.Equal("/Account/SetUpTwoFactor", login.Headers.Location?.OriginalString);

            var setupPage = await client.GetStringAsync("/Account/SetUpTwoFactor");
            Assert.Contains("data:image/png;base64,", setupPage);
            var shownKey = Regex.Match(setupPage, "<code class=\"totp-secret\">([A-Z2-7 ]+)</code>", RegexOptions.None, TimeSpan.FromSeconds(1)).Groups[1].Value.Replace(" ", "");
            var secret = await _factory.WithDbAsync(db => db.Admins.Where(a => a.AdminId == _factory.Data.AdminId).Select(a => a.TwoFactorSecret).SingleAsync());
            Assert.Equal(secret, shownKey);

            var done = await PostAsync(client, "/Account/SetUpTwoFactor", "/Account/SetUpTwoFactor",
                ("code", new Totp(Base32Encoding.ToBytes(secret!)).ComputeTotp(DateTime.UtcNow)));
            Assert.Equal(HttpStatusCode.OK, done.StatusCode);
            Assert.Contains("Save your recovery codes", await done.Content.ReadAsStringAsync());
            Assert.True(await _factory.WithDbAsync(db => db.Admins.Where(a => a.AdminId == _factory.Data.AdminId).Select(a => a.TwoFactorEnabled).SingleAsync()));
        }

        [Fact]
        public async Task AdminSessionWithoutTheAppStep_IsSentToSetItUp()
        {
            var client = _factory.ClientFor("Admin", _factory.Data.AdminId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.NoTwoFactorHeader, "1");

            var students = await client.GetAsync("/Students");
            Assert.Equal(HttpStatusCode.Redirect, students.StatusCode);
            Assert.Equal("/Profile/TwoFactor", students.Headers.Location?.OriginalString);

            var profile = await client.GetAsync("/Profile");
            Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        }

        [Fact]
        public async Task LecturerSessionsWithoutTheAppStep_AreNotAffected()
        {
            var client = _factory.ClientFor("Lecturer", _factory.Data.LecturerAId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.NoTwoFactorHeader, "1");

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Marks")).StatusCode);
        }

        [Fact]
        public async Task Admins_CannotTurnTwoFactorOff()
        {
            var data = _factory.Data;
            await _factory.WithDbAsync(db => db.Admins.Where(a => a.AdminId == data.AdminId)
                .ExecuteUpdateAsync(a => a.SetProperty(x => x.TwoFactorSecret, TwoFactor.NewSecret()).SetProperty(x => x.TwoFactorEnabled, true)));
            var client = _factory.ClientFor("Admin", data.AdminId);

            var response = await PostAsync(client, "/Profile", "/Profile/TurnOffTwoFactor", ("currentPassword", data.Password));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.True(await _factory.WithDbAsync(db => db.Admins.Where(a => a.AdminId == data.AdminId).Select(a => a.TwoFactorEnabled).SingleAsync()));
        }
    }
}
