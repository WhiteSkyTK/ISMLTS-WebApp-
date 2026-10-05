using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ISMLTS_WebApp_.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace ISMLTS.Tests.Integration
{
    // Stands in for Microsoft's answer: the sign-in name comes from a request header instead of a real token
    public class FakeMicrosoftAnswer : AuthenticationHandler<AuthenticationSchemeOptions>, IAuthenticationSignOutHandler
    {
        public const string Header = "X-Test-Microsoft-Login";

        public FakeMicrosoftAnswer(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Header, out var login)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim("preferred_username", login.ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }

        public Task SignOutAsync(AuthenticationProperties? properties) => Task.CompletedTask;
    }

    // The app with Microsoft sign-in switched on, Microsoft's endpoints faked so nothing goes over the network
    public class MicrosoftFactory : IsmltsFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Authentication:Microsoft:ClientId", "test-client");
            builder.UseSetting("Authentication:Microsoft:ClientSecret", "test-secret");
            builder.UseSetting("Authentication:Microsoft:TenantId", "college.example");
            builder.UseSetting("RateLimiting:LoginAttemptsPerMinute", "1000");
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<OpenIdConnectOptions>(MicrosoftSignIn.Scheme, options =>
                {
                    options.Configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = "https://login.example.test/college/v2.0",
                        AuthorizationEndpoint = "https://login.example.test/college/authorize",
                        TokenEndpoint = "https://login.example.test/college/token"
                    };
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(options.Configuration);
                });
                services.Configure<AuthenticationOptions>(options =>
                    options.SchemeMap[MicrosoftSignIn.ExternalScheme].HandlerType = typeof(FakeMicrosoftAnswer));
            });
        }
    }

    public class MicrosoftSignInTests : IClassFixture<MicrosoftFactory>, IClassFixture<IsmltsFactory>
    {
        private readonly MicrosoftFactory _on;
        private readonly IsmltsFactory _off;

        public MicrosoftSignInTests(MicrosoftFactory on, IsmltsFactory off)
        {
            _on = on;
            _off = off;
        }

        private static HttpRequestMessage Done(string login) =>
            new(HttpMethod.Get, "/Account/MicrosoftDone?returnUrl=%2FCalendar") { Headers = { { FakeMicrosoftAnswer.Header, login } } };

        [Theory]
        [InlineData("id", "secret", "3f2a8c1e-0000-4000-8000-000000000000", true)]
        [InlineData("id", "secret", "rosebank.example", true)]
        [InlineData("id", "secret", "common", false)]
        [InlineData("id", "secret", "Organizations", false)]
        [InlineData("id", null, "rosebank.example", false)]
        [InlineData(null, "secret", "rosebank.example", false)]
        public void OnlyOneNamedTenant_SwitchesItOn(string? clientId, string? secret, string tenant, bool on)
        {
            Assert.Equal(on, new MicrosoftSignInOptions { ClientId = clientId, ClientSecret = secret, TenantId = tenant }.IsConfigured);
        }

        [Fact]
        public async Task WhenOff_ThereIsNoButton_AndTheActionsDontExist()
        {
            var client = _off.ClientFor();

            Assert.DoesNotContain("Sign in with Microsoft", await client.GetStringAsync("/Account/Login"));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/Account/MicrosoftDone")).StatusCode);
        }

        [Fact]
        public async Task WhenOn_TheButtonGoesToTheCollegesMicrosoftSignIn()
        {
            var client = _on.ClientFor();
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, "/Account/Login");
            Assert.Contains("Sign in with Microsoft", await client.GetStringAsync("/Account/Login"));

            var response = await client.PostAsync("/Account/MicrosoftLogin?returnUrl=%2FCalendar", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location!.ToString();
            Assert.StartsWith("https://login.example.test/college/authorize?", location);
            Assert.Contains("client_id=test-client", location);
            Assert.Contains("code_challenge=", location);
            Assert.Contains(Uri.EscapeDataString("https://localhost/signin-microsoft"), location);
        }

        [Fact]
        public async Task AMatchingLecturerOrStudent_IsSignedIn()
        {
            foreach (var login in new[] { "a@lecturers.test", "S@RCCONNECT.EDU.ZA" })
            {
                var response = await _on.ClientFor().SendAsync(Done(login));

                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                Assert.Equal("/Calendar", response.Headers.Location?.OriginalString);
                Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Cookies=", StringComparison.Ordinal));
            }
        }

        [Theory]
        [InlineData("admin")]
        [InlineData("stranger@rcconnect.edu.za")]
        public async Task NoMatchingAccount_OrAnAdmin_IsNotSignedIn(string login)
        {
            var response = await _on.ClientFor().SendAsync(Done(login));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("No student or lecturer account in ISMLTS uses", await response.Content.ReadAsStringAsync());
            Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith(".AspNetCore.Cookies=", StringComparison.Ordinal)));
        }

        [Fact]
        public async Task AnAccountWithAnAuthenticator_StillEntersItsCode()
        {
            var email = await _on.WithDbAsync(async db =>
            {
                var lecturer = new ISMLTS_WebApp_.Models.Lecturer { FullName = "Two Factor", Email = "tf@lecturers.test", PasswordHash = "x", TwoFactorEnabled = true, TwoFactorSecret = "JBSWY3DPEHPK3PXP" };
                db.Lecturers.Add(lecturer);
                await db.SaveChangesAsync();
                return lecturer.Email;
            });

            var response = await _on.ClientFor().SendAsync(Done(email));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/TwoFactor", response.Headers.Location?.OriginalString);
        }
    }
}
