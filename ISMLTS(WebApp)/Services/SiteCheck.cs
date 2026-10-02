using System.Globalization;
using System.Net;
using Microsoft.Extensions.Options;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    public enum CheckState { Ok, Warning, Problem, Info }

    public record SiteCheckItem(string Area, string Name, CheckState State, string Detail);

    // Everything the checks look at, gathered once per request. Settings are only ever "set" or "not set", never their values.
    public record SiteFacts(
        string Environment,
        bool IsHttps,
        DatabaseInfo Database,
        string? RemoteIp,
        string? ForwardedFor,
        bool ForwardedHeadersEnabled,
        bool IpOnCampus,
        bool JwtKeyConfigured,
        bool TwoFactorRequiredForAdmins,
        bool EmailConfigured,
        bool EmailSiteUrlSet,
        bool LibraryLinkSet,
        bool PortalLinkSet,
        bool DemoDataOn,
        bool AppInsightsConfigured,
        TimeSpan UtcOffset,
        string TimeZone);

    // The admin Site check page: whether this server's settings and connections are ready for real use
    public static class SiteCheck
    {
        public const string SouthAfricanTimeZone = "South Africa Standard Time";

        public static List<SiteCheckItem> Evaluate(SiteFacts f)
        {
            var items = new List<SiteCheckItem>
            {
                new("Site", "Environment", CheckState.Info, f.Environment),
                f.IsHttps
                    ? new("Site", "Secure connection", CheckState.Ok, "This page came over https.")
                    : new("Site", "Secure connection", CheckState.Warning, "This page came over plain http. Turn on HTTPS Only in App Service."),
                Time(f)
            };
            items.AddRange(Database(f.Database));
            items.AddRange(Network(f));
            items.Add(f.JwtKeyConfigured
                ? new("Sign-in", "App token key", CheckState.Ok, "Jwt:SigningKey is set.")
                : new("Sign-in", "App token key", CheckState.Warning, "Jwt:SigningKey is not set, so Android app sign-ins end whenever the site restarts. Set Jwt__SigningKey (32+ characters)."));
            items.Add(f.TwoFactorRequiredForAdmins
                ? new("Sign-in", "Two-factor for admins", CheckState.Ok, "Admins must use an authenticator app.")
                : new("Sign-in", "Two-factor for admins", CheckState.Warning, "TwoFactor:RequiredForAdmins is off, so admins can sign in with a password alone."));
            items.Add(Email(f));
            items.Add(new("Links", "IIE Library", CheckState.Info, f.LibraryLinkSet ? "Shown in the shortcut row." : "Not set, so the shortcut is hidden."));
            items.Add(new("Links", "Student Portal", CheckState.Info, f.PortalLinkSet ? "Shown in the shortcut row." : "Not set, so the shortcut is hidden."));
            items.Add(f.DemoDataOn
                ? new("Data", "Demo data", CheckState.Warning, "Seed:DemoData is on. Once the demo data is in, switch it off (Seed__DemoData = false).")
                : new("Data", "Demo data", CheckState.Ok, "Off."));
            items.Add(f.AppInsightsConfigured
                ? new("Monitoring", "Application Insights", CheckState.Ok, "Errors and slow requests are sent to Application Insights.")
                : new("Monitoring", "Application Insights", CheckState.Info, "Not connected (APPLICATIONINSIGHTS_CONNECTION_STRING is not set)."));
            return items;
        }

        private static IEnumerable<SiteCheckItem> Database(DatabaseInfo db)
        {
            if (!db.Reachable)
            {
                yield return new("Database", "Connection", CheckState.Problem, "The site can't reach its database. Check the connection string and the Azure SQL firewall.");
                yield break;
            }
            yield return new("Database", "Connection", CheckState.Ok, "Connected.");
            yield return db.PendingMigrations.Count == 0
                ? new("Database", "Schema", CheckState.Ok, $"Up to date (latest migration: {db.LatestMigration ?? "none"}).")
                : new("Database", "Schema", CheckState.Problem, $"{db.PendingMigrations.Count} migration(s) not applied yet: {string.Join(", ", db.PendingMigrations)}. Restart the site to apply them.");
            yield return new("Database", "Data", CheckState.Info,
                string.Create(CultureInfo.InvariantCulture, $"{db.Students} students, {db.Lecturers} lecturers, {db.Modules} modules, {db.Courses} courses."));
        }

        private static IEnumerable<SiteCheckItem> Network(SiteFacts f)
        {
            var seen = f.RemoteIp ?? "unknown";
            var lastForwarded = LastAddress(f.ForwardedFor);
            if (f.ForwardedHeadersEnabled)
            {
                yield return new("Network", "Your address", CheckState.Ok,
                    $"The site sees {seen}, read from X-Forwarded-For ({f.ForwardedFor ?? "not sent"}).");
            }
            else if (lastForwarded != null && lastForwarded != f.RemoteIp)
            {
                yield return new("Network", "Your address", CheckState.Warning,
                    $"The site sees {seen}, but X-Forwarded-For says {lastForwarded}: that is the proxy, not you. Set ForwardedHeaders__Enabled = true so attendance scans and the log-in limit see students' real addresses.");
            }
            else
            {
                yield return new("Network", "Your address", CheckState.Ok, $"The site sees {seen}.");
            }
            yield return new("Network", "Campus network", CheckState.Info, f.IpOnCampus
                ? "Your address is in Attendance:AllowedIpRanges, so scans from here count as on campus."
                : "Your address is not in Attendance:AllowedIpRanges. Check this from the campus Wi-Fi.");
        }

        private static SiteCheckItem Time(SiteFacts f)
        {
            var offset = f.UtcOffset.ToString(f.UtcOffset < TimeSpan.Zero ? @"\-hh\:mm" : @"\+hh\:mm", CultureInfo.InvariantCulture);
            return f.UtcOffset == TimeSpan.FromHours(2)
                ? new("Site", "Time zone", CheckState.Ok, $"{f.TimeZone} (UTC{offset}).")
                : new("Site", "Time zone", CheckState.Warning, $"{f.TimeZone} (UTC{offset}). Times and due dates would be off for South Africa. Set the App Service setting WEBSITE_TIME_ZONE = {SouthAfricanTimeZone}.");
        }

        private static SiteCheckItem Email(SiteFacts f)
        {
            if (!f.EmailConfigured)
                return new("Email", "Sending", CheckState.Info, "Off: no emails are sent (Email:ConnectionString and Email:From are not set).");
            return f.EmailSiteUrlSet
                ? new("Email", "Sending", CheckState.Ok, "Emails are sent through Azure Communication Services.")
                : new("Email", "Sending", CheckState.Warning, "Emails are sent, but Email:SiteUrl is not set, so links in them won't work.");
        }

        // X-Forwarded-For lists every hop; the last one is what the front end saw. App Service adds a port ("1.2.3.4:5678").
        public static string? LastAddress(string? forwardedFor)
        {
            var last = forwardedFor?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
            if (last == null) return null;
            if (IPEndPoint.TryParse(last, out var endpoint)) return endpoint.Address.ToString();
            return IPAddress.TryParse(last, out var address) ? address.ToString() : last;
        }
    }

    public interface ISiteCheckService
    {
        Task<List<SiteCheckItem>> RunAsync(HttpContext http);
    }

    public class SiteCheckService : ISiteCheckService
    {
        private readonly IDatabaseInfoRepository _database;
        private readonly IAttendanceVerifier _verifier;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ApiSigningKey _signingKey;
        private readonly TwoFactorOptions _twoFactor;
        private readonly EmailOptions _email;
        private readonly ExternalLinksOptions _links;

        public SiteCheckService(
            IDatabaseInfoRepository database,
            IAttendanceVerifier verifier,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ApiSigningKey signingKey,
            IOptions<TwoFactorOptions> twoFactor,
            IOptions<EmailOptions> email,
            IOptions<ExternalLinksOptions> links)
        {
            _database = database;
            _verifier = verifier;
            _environment = environment;
            _configuration = configuration;
            _signingKey = signingKey;
            _twoFactor = twoFactor.Value;
            _email = email.Value;
            _links = links.Value;
        }

        public async Task<List<SiteCheckItem>> RunAsync(HttpContext http)
        {
            var remote = http.Connection.RemoteIpAddress;
            var address = remote is { IsIPv4MappedToIPv6: true } ? remote.MapToIPv4() : remote;
            // With forwarded headers on, the middleware moves the original header to X-Original-For
            var forwarded = http.Request.Headers["X-Original-For"].FirstOrDefault() ?? http.Request.Headers["X-Forwarded-For"].FirstOrDefault();

            return SiteCheck.Evaluate(new SiteFacts(
                _environment.EnvironmentName,
                http.Request.IsHttps,
                await _database.GetAsync(),
                address?.ToString(),
                forwarded,
                _configuration.GetValue<bool>("ForwardedHeaders:Enabled"),
                _verifier.IsOnCampus(address),
                !_signingKey.IsGenerated,
                _twoFactor.RequiredForAdmins,
                _email.IsConfigured,
                !string.IsNullOrWhiteSpace(_email.SiteUrl),
                ExternalLinksOptions.Usable(_links.IieLibrary) != null,
                ExternalLinksOptions.Usable(_links.StudentPortal) != null,
                _configuration.GetValue<bool>("Seed:DemoData"),
                !string.IsNullOrWhiteSpace(_configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? _configuration["ApplicationInsights:ConnectionString"]),
                TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow),
                TimeZoneInfo.Local.DisplayName));
        }
    }
}
