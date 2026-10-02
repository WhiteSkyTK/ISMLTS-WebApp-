using System.Net;

namespace ISMLTS.Tests.Integration
{
    public class HealthTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public HealthTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Health_IsPublic_AndChecksTheDatabase()
        {
            var response = await _factory.ClientFor().GetAsync(Program.HealthPath);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Health_AnswersOverPlainHttp_WithoutARedirect()
        {
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("http://localhost")
            });

            var health = await client.GetAsync(Program.HealthPath);
            var homePage = await client.GetAsync("/");

            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            Assert.Equal(HttpStatusCode.TemporaryRedirect, homePage.StatusCode);
        }
    }
}
