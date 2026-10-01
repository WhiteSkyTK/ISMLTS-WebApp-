namespace ISMLTS_WebApp_.Services
{
    // College sites the shortcut row links to (ExternalLinks section, set in App Service). A shortcut is hidden
    // while its address is empty, and only http(s) addresses are used.
    public class ExternalLinksOptions
    {
        public string? IieLibrary { get; set; }
        public string? StudentPortal { get; set; }

        public static string? Usable(string? url) => LinkValidator.IsWebLink(url) ? url!.Trim() : null;
    }
}
