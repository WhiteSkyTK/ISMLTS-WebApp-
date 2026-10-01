using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Models
{
    // What the shared _Pager partial needs, whatever the row type
    public interface IPagedList
    {
        int Page { get; }
        int TotalPages { get; }
        int TotalCount { get; }
        int FirstItem { get; }
        int LastItem { get; }
        string? Query { get; }

        // The sort the repository applied, e.g. "-name"; empty when the list has no sortable columns
        string Sort { get; }
    }

    public class PagedList<T> : IPagedList
    {
        public PagedList(IReadOnlyList<T> items, int page, int pageSize, int totalCount, string? query)
        {
            Items = items;
            PageSize = pageSize;
            TotalCount = totalCount;
            Page = Pagination.ClampPage(page, totalCount, pageSize);
            Query = query;
        }

        public IReadOnlyList<T> Items { get; }
        public int Page { get; }
        public int PageSize { get; }
        public int TotalCount { get; }
        public string? Query { get; }
        public string Sort { get; init; } = string.Empty;
        public int TotalPages => Pagination.TotalPages(TotalCount, PageSize);
        public int FirstItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
        public int LastItem => Math.Min(Page * PageSize, TotalCount);
    }
}
