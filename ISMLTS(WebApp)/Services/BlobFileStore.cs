using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Net.Http.Headers;

namespace ISMLTS_WebApp_.Services
{
    // Storage:ConnectionString, Storage:Container (default "submissions"), Storage:LocalFolder, Storage:DownloadLinkMinutes
    public class StorageOptions
    {
        public string? ConnectionString { get; set; }
        public string Container { get; set; } = "submissions";
        public string LocalFolder { get; set; } = "App_Data/submissions";
        public int DownloadLinkMinutes { get; set; } = 5;
    }

    // Azure Blob Storage (or Azurite locally): a private container; downloads are read-only links that expire after a few minutes
    public class BlobFileStore : IFileStore
    {
        private readonly BlobContainerClient _container;
        private readonly TimeSpan _linkLifetime;
        private readonly TimeProvider _time;
        private volatile bool _containerReady;

        public BlobFileStore(BlobContainerClient container, TimeSpan linkLifetime, TimeProvider time)
        {
            _container = container;
            _linkLifetime = linkLifetime;
            _time = time;
        }

        public bool IsCloud => true;

        public string Location => $"{_container.AccountName}/{_container.Name}";

        public async Task SaveAsync(string storedName, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            if (!_containerReady)
            {
                await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
                _containerReady = true;
            }
            await _container.GetBlobClient(storedName).UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                // Never overwrite: every stored name is new
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
            }, cancellationToken);
        }

        public async Task<FileDownload?> OpenAsync(string storedName, string contentType, string downloadName, CancellationToken cancellationToken = default)
        {
            var blob = _container.GetBlobClient(storedName);
            if (!await blob.ExistsAsync(cancellationToken)) return null;

            var link = DownloadLink(storedName, contentType, downloadName);
            // Signed in without an account key (no SAS possible): pass the file through the site instead
            return link != null
                ? new FileDownload(link, null)
                : new FileDownload(null, await blob.OpenReadAsync(cancellationToken: cancellationToken));
        }

        // A read-only link to one file that expires after a few minutes and saves it under the student's own file name
        public Uri? DownloadLink(string storedName, string contentType, string downloadName)
        {
            var blob = _container.GetBlobClient(storedName);
            if (!blob.CanGenerateSasUri) return null;

            var disposition = new ContentDispositionHeaderValue("attachment");
            disposition.SetHttpFileName(downloadName);
            var now = _time.GetUtcNow();
            var sas = new BlobSasBuilder(BlobSasPermissions.Read, now.Add(_linkLifetime))
            {
                BlobContainerName = _container.Name,
                BlobName = storedName,
                Resource = "b",
                StartsOn = now.AddMinutes(-5), // allows for clock drift between the site and Azure Storage
                // Azurite on a PC talks plain http; Azure itself only hands the file out over https
                Protocol = _container.Uri.Scheme == Uri.UriSchemeHttps ? SasProtocol.Https : SasProtocol.HttpsAndHttp,
                ContentType = contentType,
                ContentDisposition = disposition.ToString()
            };
            return blob.GenerateSasUri(sas);
        }

        public async Task DeleteAsync(string storedName, CancellationToken cancellationToken = default) =>
            await _container.DeleteBlobIfExistsAsync(storedName, cancellationToken: cancellationToken);

        public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _container.ExistsAsync(cancellationToken);
                return true;
            }
            catch (Exception e) when (e is RequestFailedException or AggregateException or HttpRequestException)
            {
                return false;
            }
        }
    }
}
