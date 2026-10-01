using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Extensions
{
    // The student API under /api/v1 for the Android app: bearer tokens instead of the website's cookie,
    // JSON errors instead of pages
    public static class ApiSetup
    {
        public static ApiSigningKey AddStudentApi(this WebApplicationBuilder builder)
        {
            var section = builder.Configuration.GetSection("Jwt");
            builder.Services.Configure<JwtOptions>(section);
            var jwt = section.Get<JwtOptions>() ?? new JwtOptions();
            var key = ApiSigningKey.From(jwt.SigningKey);
            builder.Services.AddSingleton(key);
            builder.Services.AddScoped<IApiTokenService, ApiTokenService>();

            builder.Services.AddAuthentication().AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                // Keep the short claim names ("sub", "role") the tokens are written with
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = key.Key,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = ApiTokenService.NameClaim,
                    RoleClaimType = ApiTokenService.RoleClaim
                };
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        return context.AuthenticateFailure is SecurityTokenExpiredException
                            ? ApiProblems.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "token_expired", "Your session has expired. Use the refresh token to get a new one.")
                            : ApiProblems.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "unauthorized", "Log in first, then send the access token as Authorization: Bearer <token>.");
                    },
                    OnForbidden = context =>
                        ApiProblems.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, "students_only", "Only students can use the app.")
                };
            });

            return key;
        }
    }
}
