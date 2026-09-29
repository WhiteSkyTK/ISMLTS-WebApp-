namespace ISMLTS_WebApp_.Models
{
    // Drives the shared _EmptyState partial shown when a list has nothing in it
    public class EmptyStateModel
    {
        public string Icon { get; set; } = "bi-inbox";
        public string Title { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? ActionUrl { get; set; }
        public string? ActionText { get; set; }
    }
}
