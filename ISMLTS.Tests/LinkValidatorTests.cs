using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class LinkValidatorTests
    {
        [Theory]
        [InlineData("https://github.com/student/poe")]
        [InlineData("http://drive.google.com/file/d/abc")]
        public void IsWebLink_AcceptsHttpAndHttps(string link) =>
            Assert.True(LinkValidator.IsWebLink(link));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("javascript:alert(1)")]
        [InlineData("JavaScript:alert(1)")]
        [InlineData("data:text/html,<script>alert(1)</script>")]
        [InlineData("ftp://files.example.com/poe.zip")]
        [InlineData("github.com/student/poe")]
        [InlineData("/Account/Logout")]
        public void IsWebLink_RejectsEverythingElse(string? link) =>
            Assert.False(LinkValidator.IsWebLink(link));

        [Fact]
        public void IsWebLink_RejectsLinksLongerThanTheColumn() =>
            Assert.False(LinkValidator.IsWebLink("https://example.com/" + new string('a', LinkValidator.MaxLength)));
    }
}
