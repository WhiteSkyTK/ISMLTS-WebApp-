using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace ISMLTS.Tests.Integration
{
    // The app with an Application Insights connection string, as on App Service once monitoring is connected
    public class MonitoringFactory : IsmltsFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting(Program.MonitoringConnectionSetting, "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost/");
        }
    }

    public class MonitoringTests : IClassFixture<MonitoringFactory>
    {
        private readonly MonitoringFactory _factory;

        public MonitoringTests(MonitoringFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task WithAConnectionString_TheSiteStillStartsAndAnswers()
        {
            Assert.Equal(HttpStatusCode.OK, (await _factory.ClientFor().GetAsync(Program.HealthPath)).StatusCode);
            Assert.Contains("Errors and slow requests are sent to Application Insights.", await _factory.ClientFor("Admin", _factory.Data.AdminId).GetStringAsync("/SiteCheck"));
        }
    }
}
