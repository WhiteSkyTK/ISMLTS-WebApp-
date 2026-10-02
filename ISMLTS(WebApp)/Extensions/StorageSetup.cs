using Azure.Core;
using Azure.Storage.Blobs;
using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Extensions
{
    // Uploaded submissions go to Azure Blob Storage when Storage:ConnectionString is set (UseDevelopmentStorage=true for
    // Azurite), otherwise to Storage:LocalFolder under the app's folder (never under wwwroot)
    public static class StorageSetup
    {
        public static void AddSubmissionStorage(this WebApplicationBuilder builder)
        {
            builder.Services.Configure<SubmissionFileOptions>(builder.Configuration.GetSection("Submissions"));
            var storage = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();

            if (!string.IsNullOrWhiteSpace(storage.ConnectionString))
            {
                var clientOptions = new BlobClientOptions();
                // Fail fast enough that an upload page or the Site check doesn't hang when storage is unreachable
                clientOptions.Retry.MaxRetries = 2;
                clientOptions.Retry.Mode = RetryMode.Exponential;
                clientOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(30);
                var container = new BlobContainerClient(storage.ConnectionString, storage.Container, clientOptions);
                var lifetime = TimeSpan.FromMinutes(Math.Clamp(storage.DownloadLinkMinutes, 1, 60));
                builder.Services.AddSingleton<IFileStore>(services => new BlobFileStore(container, lifetime, services.GetRequiredService<TimeProvider>()));
            }
            else
            {
                var folder = Path.IsPathRooted(storage.LocalFolder)
                    ? storage.LocalFolder
                    : Path.Combine(builder.Environment.ContentRootPath, storage.LocalFolder);
                builder.Services.AddSingleton<IFileStore>(new LocalFileStore(folder));
            }

            builder.Services.AddScoped<ISubmissionFileService, SubmissionFileService>();
        }
    }
}
