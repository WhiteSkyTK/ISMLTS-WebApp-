using System.Linq.Expressions;

namespace ISMLTS_WebApp_.Services
{
    // The ?sort= value of a paged admin list: a column key, with a leading "-" for descending ("name", "-name").
    // Only keys a repository allows are used, so the query string never reaches the database as-is.
    public readonly record struct ListSort(string Key, bool Descending)
    {
        public static ListSort Parse(string? value, string defaultKey, params string[] allowedKeys)
        {
            var sort = Read(value);
            return allowedKeys.Contains(sort.Key) ? sort : new ListSort(defaultKey, false);
        }

        // Reads a sort the repository has already checked (PagedList.Sort)
        public static ListSort Read(string? value)
        {
            var descending = value?.StartsWith('-') == true;
            return new ListSort((descending ? value![1..] : value) ?? string.Empty, descending);
        }

        // What a column header links to: the other direction when it's already the sort column, ascending otherwise
        public string LinkFor(string key) => Key == key && !Descending ? $"-{key}" : key;

        public override string ToString() => Descending ? $"-{Key}" : Key;
    }

    public static class ListSortExtensions
    {
        public static IOrderedQueryable<T> OrderBy<T, TKey>(this IQueryable<T> source, Expression<Func<T, TKey>> key, bool descending) =>
            descending ? source.OrderByDescending(key) : source.OrderBy(key);
    }
}
