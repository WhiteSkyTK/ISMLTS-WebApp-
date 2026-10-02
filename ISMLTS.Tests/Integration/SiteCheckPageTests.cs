using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace ISMLTS.Tests.Integration
{
    // The app with ForwardedHeaders:Enabled, as on App Service when the front end passes the client's address on
    public class ForwardedHeadersFactory : IsmltsFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("ForwardedHeaders:Enabled", "true");
        }
    }

    public class SiteCheckPageTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public SiteCheckPageTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Admins_SeeTheChecks()
        {
            var html = await _factory.ClientFor("Admin", _factory.Data.AdminId).GetStringAsync("/SiteCheck");

            Assert.Contains("Site check", html);
            Assert.Contains("Connected.", html);
            Assert.Contains("App token key", html);
            Assert.Contains("Time zone", html);
        }

        [Fact]
        public async Task WithoutForwardedHeaders_TheProxyAddressIsFlagged()
        {
            var client = _factory.ClientFor("Admin", _factory.Data.AdminId);
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "41.13.20.5:51234");

            var html = await client.GetStringAsync("/SiteCheck");

            Assert.Contains("but X-Forwarded-For says 41.13.20.5", html);
        }

        [Theory]
        [InlineData("Lecturer")]
        [InlineData("Student")]
        public async Task OnlyAdmins_CanOpenIt(string role)
        {
            var id = role == "Lecturer" ? _factory.Data.LecturerAId : _factory.Data.StudentId;

            var response = await _factory.ClientFor(role, id).GetAsync("/SiteCheck");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/AccessDenied", response.Headers.Location?.PathAndQuery);
        }
    }

    public class ForwardedHeadersTests : IClassFixture<ForwardedHeadersFactory>
    {
        private readonly ForwardedHeadersFactory _factory;

        public ForwardedHeadersTests(ForwardedHeadersFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task WhenSwitchedOn_TheSiteSeesTheLastForwardedAddress()
        {
            var client = _factory.ClientFor("Admin", _factory.Data.AdminId);
            client.DefaultRequestHeaders.Add("X-Forwarded-For", "6.6.6.6, 41.13.20.5:51234");

            var html = await client.GetStringAsync("/SiteCheck");

            Assert.Contains("The site sees 41.13.20.5, read from X-Forwarded-For", html);
            Assert.DoesNotContain("The site sees 6.6.6.6", html);
        }
    }
}
