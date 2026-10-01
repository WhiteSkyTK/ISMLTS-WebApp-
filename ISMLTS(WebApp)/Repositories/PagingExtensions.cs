using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Repositories
{
    public static class PagingExtensions
    {
        public static async Task<PagedList<T>> ToPagedListAsync<T>(this IQueryable<T> source, int page, string? query, ListSort? sort = null)
        {
            var totalCount = await source.CountAsync();
            var safePage = Pagination.ClampPage(page, totalCount, Pagination.PageSize);
            var items = await source.Skip((safePage - 1) * Pagination.PageSize).Take(Pagination.PageSize).ToListAsync();
            return new PagedList<T>(items, safePage, Pagination.PageSize, totalCount, query) { Sort = sort?.ToString() ?? string.Empty };
        }
    }
}
