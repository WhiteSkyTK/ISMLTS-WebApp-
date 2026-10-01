using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests.Integration
{
    public class ExternalLinksTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public ExternalLinksTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task CollegeLinks_AreHidden_WhileTheyHaveNoAddress()
        {
            var html = await _factory.ClientFor("Student", _factory.Data.StudentId).GetStringAsync("/Help");

            Assert.DoesNotContain("IIE Library", html);
            Assert.DoesNotContain("Student Portal", html);
        }

        [Theory]
        [InlineData("https://library.example.ac.za", "https://library.example.ac.za")]
        [InlineData(" https://portal.example.ac.za ", "https://portal.example.ac.za")]
        [InlineData("", null)]
        [InlineData(null, null)]
        [InlineData("javascript:alert(1)", null)]
        [InlineData("ftp://files.example.ac.za", null)]
        public void OnlyWebAddresses_AreUsed(string? configured, string? expected)
        {
            Assert.Equal(expected, ExternalLinksOptions.Usable(configured));
        }
    }
}
