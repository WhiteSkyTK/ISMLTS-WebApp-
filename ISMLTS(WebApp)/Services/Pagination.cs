namespace ISMLTS_WebApp_.Services
{
    public static class Pagination
    {
        public const int PageSize = 25;
        private const int WindowSize = 5;

        public static int TotalPages(int totalCount, int pageSize) =>
            Math.Max(1, (totalCount + pageSize - 1) / pageSize);

        // Page numbers from the query string can be anything; keep them inside 1..TotalPages
        public static int ClampPage(int page, int totalCount, int pageSize) =>
            Math.Clamp(page, 1, TotalPages(totalCount, pageSize));

        // Up to five page numbers centred on the current page, e.g. 3 4 [5] 6 7
        public static IReadOnlyList<int> Window(int page, int totalPages)
        {
            var first = Math.Max(1, Math.Min(page - (WindowSize / 2), totalPages - WindowSize + 1));
            var last = Math.Min(totalPages, first + WindowSize - 1);
            return Enumerable.Range(first, last - first + 1).ToList();
        }
    }
}
