using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class StatusPagesTests
    {
        [Theory]
        [InlineData(400, "That request didn't work")]
        [InlineData(403, "You don't have access to this page")]
        [InlineData(404, "Page not found")]
        [InlineData(429, "Too many attempts")]
        [InlineData(500, "Something went wrong")]
        public void Describe_GivesEachCommonCodeItsOwnTitle(int code, string title) =>
            Assert.Equal(title, StatusPages.Describe(code).Title);

        [Fact]
        public void Describe_ShowsMethodNotAllowedAsNotFound() =>
            Assert.Equal(404, StatusPages.Describe(405).Code);

        [Theory]
        [InlineData(200)]
        [InlineData(999)]
        public void Describe_TreatsCodesOutsideTheErrorRangeAsServerErrors(int code) =>
            Assert.Equal(500, StatusPages.Describe(code).Code);
    }
}
