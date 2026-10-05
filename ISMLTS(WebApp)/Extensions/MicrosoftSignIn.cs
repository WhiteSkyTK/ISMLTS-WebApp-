using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace ISMLTS_WebApp_.Extensions
{
    // Authentication:Microsoft: an app registration in the college's Microsoft Entra ID tenant
    public class MicrosoftSignInOptions
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? TenantId { get; set; }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret) && IsCollegeTenant(TenantId);

        // "common", "organizations" or "consumers" would accept accounts from any organisation, whose sign-in names the
        // college doesn't control; only one named tenant (its ID or domain) is accepted
        public static bool IsCollegeTenant(string? tenant) =>
            !string.IsNullOrWhiteSpace(tenant) && tenant.Trim().ToLowerInvariant() is not ("common" or "organizations" or "consumers");
    }

    // Optional "Sign in with Microsoft" (OpenID Connect). It only signs in an existing student or lecturer whose college
    // email is the Microsoft account's sign-in name; it never creates accounts, and password log-in stays.
    public static class MicrosoftSignIn
    {
        public const string Scheme = "Microsoft";
        public const string ExternalScheme = "MicrosoftExternal";
        public const string CallbackPath = "/signin-microsoft";
        public const string FailedQuery = "microsoft";

        public static void AddMicrosoftSignIn(this WebApplicationBuilder builder)
        {
            var section = builder.Configuration.GetSection("Authentication:Microsoft");
            builder.Services.Configure<MicrosoftSignInOptions>(section);
            var settings = section.Get<MicrosoftSignInOptions>() ?? new MicrosoftSignInOptions();
            if (!settings.IsConfigured) return;

            builder.Services.AddAuthentication()
                // Holds Microsoft's answer for a moment, until the site has matched it to an account
                .AddCookie(ExternalScheme, options =>
                {
                    options.Cookie.Name = ".ISMLTS.Microsoft";
                    options.Cookie.HttpOnly = true;
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                    options.SlidingExpiration = false;
                })
                .AddOpenIdConnect(Scheme, "Microsoft", options =>
                {
                    options.Authority = $"https://login.microsoftonline.com/{settings.TenantId!.Trim()}/v2.0";
                    options.ClientId = settings.ClientId;
                    options.ClientSecret = settings.ClientSecret;
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.UsePkce = true;
                    options.CallbackPath = CallbackPath;
                    options.SignInScheme = ExternalScheme;
                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    options.MapInboundClaims = false;
                    options.SaveTokens = false;
                    // Cancelled or refused at Microsoft: back to the log-in page with a message instead of an error page
                    options.Events.OnRemoteFailure = context =>
                    {
                        context.Response.Redirect($"/Account/Login?{FailedQuery}=failed");
                        context.HandleResponse();
                        return Task.CompletedTask;
                    };
                });
        }

        // The account's sign-in name in the college's tenant, e.g. st10000001@rcconnect.edu.za
        public static string? Login(ClaimsPrincipal principal) =>
            (principal.FindFirst("preferred_username") ?? principal.FindFirst("upn") ?? principal.FindFirst("email"))?.Value?.Trim();
    }
}
