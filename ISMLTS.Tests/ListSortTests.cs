using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class ListSortTests
    {
        [Theory]
        [InlineData("email", "email", false)]
        [InlineData("-email", "email", true)]
        [InlineData(null, "name", false)]
        [InlineData("", "name", false)]
        [InlineData("-", "name", false)]
        [InlineData("PasswordHash", "name", false)]
        [InlineData("-name; DROP TABLE Students", "name", false)]
        public void Parse_OnlyAcceptsAllowedKeys(string? value, string key, bool descending)
        {
            Assert.Equal(new ListSort(key, descending), ListSort.Parse(value, "name", "name", "email"));
        }

        [Fact]
        public void LinkFor_FlipsTheActiveColumn_AndStartsOthersAscending()
        {
            var byNameAscending = ListSort.Read("name");

            Assert.Equal("-name", byNameAscending.LinkFor("name"));
            Assert.Equal("name", ListSort.Read("-name").LinkFor("name"));
            Assert.Equal("email", byNameAscending.LinkFor("email"));
        }

        [Fact]
        public void ToString_RoundTripsThroughRead()
        {
            Assert.Equal("-modules", ListSort.Read("-modules").ToString());
            Assert.Equal("code", new ListSort("code", false).ToString());
        }
    }
}
