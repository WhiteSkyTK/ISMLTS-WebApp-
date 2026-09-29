using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class PasswordRulesTests
    {
        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("        ", false)]
        [InlineData("Short1!", false)]
        [InlineData("Exactly8", true)]
        [InlineData("a much longer passphrase", true)]
        public void IsLongEnough_NeedsEightNonBlankCharacters(string? password, bool expected) =>
            Assert.Equal(expected, PasswordRules.IsLongEnough(password));
    }
}
