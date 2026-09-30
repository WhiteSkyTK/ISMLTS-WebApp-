namespace ISMLTS_WebApp_.Models
{
    // A "Show" dropdown next to a table's search box. Rows carry data-{Name}="token token"; picking an option keeps
    // the rows whose tokens include its value. Views pass these to _TableFilter through ViewData["TableFacets"].
    public class TableFacet
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string AllText { get; set; } = "All";
        public List<(string Value, string Text)> Options { get; set; } = new();
    }
}
