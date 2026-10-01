using ISMLTS_WebApp_.Models;

namespace ISMLTS.Tests
{
    public class TableFacetTests
    {
        [Theory]
        [InlineData("Diploma in Software Development", "diploma-in-software-development")]
        [InlineData("Web Development (Intermediate)", "web-development-intermediate")]
        [InlineData("  BSc  IT ", "bsc-it")]
        [InlineData(null, "")]
        public void Token_MakesOneLowercaseWordWithoutSpaces(string? text, string expected)
        {
            Assert.Equal(expected, TableFacet.Token(text));
        }
    }
}
