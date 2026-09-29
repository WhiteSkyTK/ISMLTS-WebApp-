using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Services
{
    // Friendly wording for the status codes UseStatusCodePagesWithReExecute sends to /Status/{code}
    public static class StatusPages
    {
        public static StatusPageModel Describe(int code) => code switch
        {
            400 => new(400, "That request didn't work",
                "The form may have expired. Go back, refresh the page and try again.", "bi-arrow-counterclockwise"),
            403 => new(403, "You don't have access to this page",
                "It belongs to a different role. If you think you should see it, ask an admin.", "bi-shield-lock"),
            404 or 405 => new(404, "Page not found",
                "The page doesn't exist, or it belongs to someone else's module.", "bi-signpost-split"),
            429 => new(429, "Too many attempts",
                "Wait a minute, then try again.", "bi-hourglass-split"),
            _ => new(code is >= 400 and <= 599 ? code : 500, "Something went wrong",
                "It's not you, it's us. Try again in a moment.", "bi-exclamation-octagon")
        };
    }
}
