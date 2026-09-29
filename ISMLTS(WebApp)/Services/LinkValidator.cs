namespace ISMLTS_WebApp_.Services
{
    public static class LinkValidator
    {
        public const int MaxLength = 300;

        // Only absolute http(s) links, so a stored "javascript:" link can't run when a lecturer clicks it
        public static bool IsWebLink(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && value.Length <= MaxLength
            && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }
}
