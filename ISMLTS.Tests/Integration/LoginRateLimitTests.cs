using System.Net;

namespace ISMLTS.Tests.Integration
{
    // Own factory, so these attempts don't use up the limit for other test classes
    public class LoginRateLimitTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public LoginRateLimitTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task SixthLoginAttemptInAMinute_GetsAFriendlyTooManyRequestsPage()
        {
            var client = _factory.ClientFor();
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, "/Account/Login");

            for (var attempt = 1; attempt <= 5; attempt++)
            {
                var allowed = await PostLoginAsync(client, token, "wrong-password");
                Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
                Assert.Contains("Invalid login attempt.", await allowed.Content.ReadAsStringAsync());
            }

            var blocked = await PostLoginAsync(client, token, _factory.Data.Password);

            Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
            Assert.True(blocked.Headers.Contains("Retry-After"));
            Assert.Contains("Too many attempts", await blocked.Content.ReadAsStringAsync());
        }

        private Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string token, string password) =>
            client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["EmailOrUsername"] = _factory.Data.StudentEmail,
                ["Password"] = password
            }));
    }
}
