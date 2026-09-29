using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class PaginationTests
    {
        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(25, 1)]
        [InlineData(26, 2)]
        [InlineData(100, 4)]
        public void TotalPages_AlwaysHasAtLeastOnePage(int totalCount, int expected) =>
            Assert.Equal(expected, Pagination.TotalPages(totalCount, 25));

        [Theory]
        [InlineData(-3, 1)]
        [InlineData(0, 1)]
        [InlineData(2, 2)]
        [InlineData(99, 4)]
        public void ClampPage_KeepsQueryStringPagesInRange(int requested, int expected) =>
            Assert.Equal(expected, Pagination.ClampPage(requested, 100, 25));

        [Theory]
        [InlineData(1, 2, new[] { 1, 2 })]
        [InlineData(1, 9, new[] { 1, 2, 3, 4, 5 })]
        [InlineData(5, 9, new[] { 3, 4, 5, 6, 7 })]
        [InlineData(9, 9, new[] { 5, 6, 7, 8, 9 })]
        public void Window_ShowsUpToFivePagesAroundTheCurrentOne(int page, int totalPages, int[] expected) =>
            Assert.Equal(expected, Pagination.Window(page, totalPages));

        [Fact]
        public void PagedList_DescribesTheRowsShown()
        {
            var list = new PagedList<string>(new[] { "x" }, page: 2, pageSize: 25, totalCount: 26, query: null);

            Assert.Equal(2, list.TotalPages);
            Assert.Equal(26, list.FirstItem);
            Assert.Equal(26, list.LastItem);
        }

        [Fact]
        public void PagedList_WithNoRows_ShowsNothing()
        {
            var list = new PagedList<string>(Array.Empty<string>(), page: 1, pageSize: 25, totalCount: 0, query: "nobody");

            Assert.Equal(0, list.FirstItem);
            Assert.Equal(0, list.LastItem);
            Assert.Equal(1, list.TotalPages);
        }
    }
}
