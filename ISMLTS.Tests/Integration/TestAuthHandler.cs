using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISMLTS.Tests.Integration
{
    // Signs a request in as whatever role and user id its headers name; no headers means anonymous.
    // Challenge and forbid still go through the app's cookie scheme, so redirects match production.
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string RoleHeader = "X-Test-Role";
        public const string UserIdHeader = "X-Test-UserId";

        // Test users count as having passed the authenticator step unless this header is sent
        public const string NoTwoFactorHeader = "X-Test-NoTwoFactor";

        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RoleHeader, out var role) || !Request.Headers.TryGetValue(UserIdHeader, out var userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Name, $"Test {role}"),
                new(ClaimTypes.Role, role.ToString())
            };
            if (!Request.Headers.ContainsKey(NoTwoFactorHeader))
            {
                claims.Add(new Claim(ISMLTS_WebApp_.Services.AccountService.TwoFactorClaim, "true"));
            }
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
