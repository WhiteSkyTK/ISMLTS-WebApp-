namespace ISMLTS_WebApp_.Models
{
    public static class ListFilters
    {
        // Dropdown value for "not set" (no programme, no course)
        public const string None = "none";
    }

    // Filters on the admin Modules list; CourseId 0 means modules that aren't in a course
    public record ModuleListFilter(int? CourseId = null, string? Term = null, int? LecturerId = null);

    // A dropdown in _ListSearch that filters a paged admin list on the server; views pass these through ViewData["ListFilters"]
    public class ListFilter
    {
        // The query string key, e.g. "programme"
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string AllText { get; set; } = "All";
        public string? Selected { get; set; }
        public List<(string Value, string Text)> Options { get; set; } = new();
    }

    // One clickable column header on a paged admin list
    public class SortHeaderModel
    {
        public string Label { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;

        // The sort the list actually used (PagedList.Sort), e.g. "-name"
        public string Current { get; set; } = string.Empty;
    }
}
