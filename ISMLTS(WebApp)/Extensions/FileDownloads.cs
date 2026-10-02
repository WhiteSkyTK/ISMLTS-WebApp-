using Microsoft.AspNetCore.Mvc;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Extensions
{
    public static class FileDownloads
    {
        // Blob Storage: send the browser (or app) to a read-only link that expires in minutes.
        // Local folder: stream the file as an attachment, so the browser never shows it as part of the site.
        public static IActionResult SubmissionFile(this ControllerBase controller, SubmissionFile file, FileDownload download)
        {
            var response = controller.Response;
            response.Headers.CacheControl = "no-store";
            if (download.RedirectTo != null) return controller.Redirect(download.RedirectTo.AbsoluteUri);

            response.Headers.XContentTypeOptions = "nosniff";
            return controller.File(download.Content!, file.ContentType, file.FileName);
        }
    }
}
