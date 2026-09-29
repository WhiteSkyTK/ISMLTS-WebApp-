namespace ISMLTS_WebApp_.Models
{
    // Drives the shared _PageHeader partial: title, optional subtitle, back link and one primary action
    public class PageHeaderModel
    {
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? BackUrl { get; set; }
        public string BackText { get; set; } = "Back";
        public string? ActionUrl { get; set; }
        public string? ActionText { get; set; }
        public string ActionIcon { get; set; } = "bi-plus-lg";
    }
}
