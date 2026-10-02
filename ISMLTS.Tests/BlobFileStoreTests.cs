using Azure.Storage.Blobs;
using ISMLTS_WebApp_.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace ISMLTS.Tests
{
    // The download links Blob Storage hands out; building one needs no network
    public class BlobFileStoreTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);

        private sealed class FixedTime : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => Now;
        }

        // A made-up account; the key only has to be valid base64
        private static BlobContainerClient Container() => new(
            $"DefaultEndpointsProtocol=https;AccountName=ismltstest;AccountKey={Convert.ToBase64String(new byte[64])};EndpointSuffix=core.windows.net",
            "submissions");

        [Fact]
        public void DownloadLink_ReadsOneFile_OverHttps_ForFiveMinutes_UnderTheStudentsFileName()
        {
            var store = new BlobFileStore(Container(), TimeSpan.FromMinutes(5), new FixedTime());

            var link = store.DownloadLink("12/abc.pdf", "application/pdf", "My Work.pdf");

            Assert.NotNull(link);
            var query = QueryHelpers.ParseQuery(link.Query);
            Assert.Equal(("https", "/submissions/12/abc.pdf"), (link.Scheme, link.AbsolutePath));
            Assert.Equal("r", query["sp"]);
            Assert.Equal("b", query["sr"]);
            Assert.Equal("https", query["spr"]);
            Assert.Equal("2026-10-02T08:05:00Z", query["se"]);
            Assert.Equal("application/pdf", query["rsct"]);
            Assert.StartsWith("attachment; filename=\"My Work.pdf\"", query["rscd"].ToString());
        }

        [Fact]
        public void WithoutAnAccountKey_ThereIsNoLink_SoTheSitePassesTheFileThrough()
        {
            var store = new BlobFileStore(new BlobContainerClient(new Uri("https://ismltstest.blob.core.windows.net/submissions")), TimeSpan.FromMinutes(5), new FixedTime());

            Assert.Null(store.DownloadLink("12/abc.pdf", "application/pdf", "work.pdf"));
            Assert.True(store.IsCloud);
            Assert.Equal("ismltstest/submissions", store.Location);
        }
    }
}
