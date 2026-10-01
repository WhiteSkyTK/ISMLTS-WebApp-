using System.Globalization;
using System.Threading.RateLimiting;
using ISMLTS_WebApp_.Controllers;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Filters;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Integration tests run as "Testing": they register their own SQLite database and seed it themselves
var isTesting = builder.Environment.IsEnvironment("Testing");
if (!isTesting)
{
    var connectionString = builder.Configuration.GetConnectionString("ApplicationDbContext")
        ?? throw new InvalidOperationException("Connection string 'ApplicationDbContext' not found.");
    builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    })
    // Holds who passed the password step while they enter their authenticator code; never grants access on its own
    .AddCookie(AccountController.TwoFactorScheme, options =>
    {
        options.Cookie.Name = ".ISMLTS.TwoFactor";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        options.SlidingExpiration = false;
        options.LoginPath = "/Account/Login";
    });

builder.Services.AddControllersWithViews(options => options.Filters.Add<AccountSetupFilter>());

// JSON API for the Android app (bearer tokens, /api/v1)
var apiSigningKey = builder.AddStudentApi();

// Slows password guessing on the login form. Campus Wi-Fi may put a whole class behind one IP,
// so the limit is a setting that can be raised in App Service without a redeploy.
var loginAttemptsPerMinute = builder.Configuration.GetValue("RateLimiting:LoginAttemptsPerMinute", 5);
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(AccountController.LoginRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = loginAttemptsPerMinute, Window = TimeSpan.FromMinutes(1) }));

    // A friendly page (or JSON for the app) instead of a bare 429
    options.OnRejected = async (context, _) =>
    {
        var http = context.HttpContext;
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            http.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
        if (ApiProblems.IsApiRequest(http))
        {
            await ApiProblems.WriteAsync(http, StatusCodes.Status429TooManyRequests, "too_many_attempts", "Too many log-in attempts. Wait a minute and try again.");
            return;
        }
        var view = new ViewResult { ViewName = "~/Views/Account/TooManyAttempts.cshtml" };
        await view.ExecuteResultAsync(new ActionContext(http, http.GetRouteData(), new ActionDescriptor()));
    };
});

builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<ILecturerRepository, LecturerRepository>();
builder.Services.AddScoped<IModuleRepository, ModuleRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IMarkRepository, MarkRepository>();
builder.Services.AddScoped<IAssessmentRepository, AssessmentRepository>();
builder.Services.AddScoped<ISubmissionRepository, SubmissionRepository>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationSettingRepository, NotificationSettingRepository>();
builder.Services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
builder.Services.AddScoped<IMarkChangeRepository, MarkChangeRepository>();
builder.Services.AddScoped<IApiRefreshTokenRepository, ApiRefreshTokenRepository>();

builder.Services.Configure<AttendanceOptions>(builder.Configuration.GetSection("Attendance"));
builder.Services.Configure<RiskOptions>(builder.Configuration.GetSection("Risk"));
builder.Services.Configure<StudentOptions>(builder.Configuration.GetSection("Students"));
builder.Services.Configure<TwoFactorOptions>(builder.Configuration.GetSection("TwoFactor"));
builder.Services.Configure<ExternalLinksOptions>(builder.Configuration.GetSection("ExternalLinks"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAttendanceVerifier, AttendanceVerifier>();
builder.Services.AddSingleton<IQrCodeService, QrCodeService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IUserImportService, UserImportService>();
builder.Services.AddScoped<IStudentPortalService, StudentPortalService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IMarkService, MarkService>();

// Email goes through Azure Communication Services only when Email:ConnectionString and Email:From are set
var emailSection = builder.Configuration.GetSection("Email");
builder.Services.Configure<EmailOptions>(emailSection);
if (emailSection.Get<EmailOptions>()?.IsConfigured == true)
{
    builder.Services.AddSingleton<IEmailSender, AcsEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, NullEmailSender>();
}
builder.Services.AddScoped<IWeeklyDigestSender, WeeklyDigestSender>();
if (!isTesting)
{
    builder.Services.AddHostedService<WeeklyDigestService>();
}

var app = builder.Build();

if (!isTesting)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DataSeeder.SeedAsync(db, app.Configuration);
}

if (apiSigningKey.IsGenerated && !isTesting)
{
    app.Logger.LogWarning("Jwt:SigningKey is not set, so app sign-ins end whenever the site restarts. Set Jwt__SigningKey (32+ characters).");
}

if (!app.Environment.IsDevelopment())
{
    // The app gets a JSON problem; the website gets the error page
    app.UseWhen(ApiProblems.IsApiRequest, api => api.UseExceptionHandler(error => error.Run(http =>
        ApiProblems.WriteAsync(http, StatusCodes.Status500InternalServerError, "server_error", "Something went wrong on the server. Try again later."))));
    app.UseWhen(http => !ApiProblems.IsApiRequest(http), site => site.UseExceptionHandler("/Home/Error"));
    app.UseHsts();
}

// Empty 400/404/405 responses (NotFound(), bad antiforgery tokens, unknown URLs) get a styled page; the API answers in JSON itself
app.UseWhen(http => !ApiProblems.IsApiRequest(http), site => site.UseStatusCodePagesWithReExecute("/Status/{0}"));

app.UseHttpsRedirection();

// Forces "." as the decimal separator for posted numbers (marks, GPS), whatever the server's regional settings.
app.UseRequestLocalization("en-US");

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await app.RunAsync();

// Lets ISMLTS.Tests start the app with WebApplicationFactory<Program>
public partial class Program;