using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class SiteCheckTests
    {
        private static readonly DatabaseInfo GoodDatabase = new(true, Array.Empty<string>(), "20261001114917_Phase5ApiTokens", 30, 4, 16, 3);

        private static SiteFacts Live(Func<SiteFacts, SiteFacts>? change = null)
        {
            var facts = new SiteFacts(
                "Production", true, GoodDatabase, "41.13.20.5", "41.13.20.5:51234", false, false,
                JwtKeyConfigured: true, TwoFactorRequiredForAdmins: true, EmailConfigured: false, EmailSiteUrlSet: false,
                LibraryLinkSet: false, PortalLinkSet: false, DemoDataOn: false, AppInsightsConfigured: true,
                TimeSpan.FromHours(2), "(UTC+02:00) Harare, Pretoria");
            return change == null ? facts : change(facts);
        }

        private static SiteCheckItem Find(List<SiteCheckItem> items, string name) => items.Single(i => i.Name == name);

        [Fact]
        public void AWellSetUpSite_HasNoProblemsOrWarnings()
        {
            var items = SiteCheck.Evaluate(Live());

            Assert.DoesNotContain(items, i => i.State is CheckState.Problem or CheckState.Warning);
        }

        [Fact]
        public void AnUnreachableDatabase_IsAProblem_AndSkipsTheOtherDatabaseChecks()
        {
            var items = SiteCheck.Evaluate(Live(f => f with { Database = new DatabaseInfo(false, Array.Empty<string>(), null, 0, 0, 0, 0) }));

            Assert.Equal(CheckState.Problem, Find(items, "Connection").State);
            Assert.DoesNotContain(items, i => i.Name == "Schema");
        }

        [Fact]
        public void PendingMigrations_AreNamed()
        {
            var items = SiteCheck.Evaluate(Live(f => f with { Database = GoodDatabase with { PendingMigrations = new[] { "20261001114917_Phase5ApiTokens" } } }));

            var schema = Find(items, "Schema");
            Assert.Equal(CheckState.Problem, schema.State);
            Assert.Contains("Phase5ApiTokens", schema.Detail);
        }

        [Fact]
        public void TheProxysAddress_IsFlagged_UntilForwardedHeadersAreOn()
        {
            var behindProxy = Live(f => f with { RemoteIp = "10.0.0.4", ForwardedFor = "41.13.20.5:51234" });

            Assert.Equal(CheckState.Warning, Find(SiteCheck.Evaluate(behindProxy), "Your address").State);
            Assert.Equal(CheckState.Ok, Find(SiteCheck.Evaluate(behindProxy with { ForwardedHeadersEnabled = true }), "Your address").State);
        }

        [Fact]
        public void AServerOnUtc_IsToldToSetTheTimeZone()
        {
            var item = Find(SiteCheck.Evaluate(Live(f => f with { UtcOffset = TimeSpan.Zero, TimeZone = "(UTC) Coordinated Universal Time" })), "Time zone");

            Assert.Equal(CheckState.Warning, item.State);
            Assert.Contains("WEBSITE_TIME_ZONE", item.Detail);
        }

        [Theory]
        [InlineData(false, true, CheckState.Warning)]
        [InlineData(true, false, CheckState.Warning)]
        [InlineData(true, true, CheckState.Warning)]
        public void MissingKeysAndSettings_AreFlagged(bool jwtKey, bool twoFactor, CheckState expected)
        {
            var items = SiteCheck.Evaluate(Live(f => f with { JwtKeyConfigured = jwtKey, TwoFactorRequiredForAdmins = twoFactor, DemoDataOn = jwtKey && twoFactor }));

            Assert.Contains(items, i => i.State == expected);
        }

        [Fact]
        public void EmailWithoutASiteUrl_IsFlagged()
        {
            Assert.Equal(CheckState.Info, Find(SiteCheck.Evaluate(Live()), "Sending").State);
            Assert.Equal(CheckState.Warning, Find(SiteCheck.Evaluate(Live(f => f with { EmailConfigured = true })), "Sending").State);
            Assert.Equal(CheckState.Ok, Find(SiteCheck.Evaluate(Live(f => f with { EmailConfigured = true, EmailSiteUrlSet = true })), "Sending").State);
        }

        [Theory]
        [InlineData("41.13.20.5:51234", "41.13.20.5")]
        [InlineData("203.0.113.9, 41.13.20.5:51234", "41.13.20.5")]
        [InlineData("41.13.20.5", "41.13.20.5")]
        [InlineData("[2001:db8::1]:443", "2001:db8::1")]
        [InlineData(null, null)]
        [InlineData("", null)]
        public void LastAddress_ReadsTheLastHopWithoutItsPort(string? header, string? expected)
        {
            Assert.Equal(expected, SiteCheck.LastAddress(header));
        }
    }
}
