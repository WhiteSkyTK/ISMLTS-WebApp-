using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    // Submissions:MaxFileMegabytes; App Service (IIS) refuses requests above about 28 MB, so the limit stops at 25
    public class SubmissionFileOptions
    {
        public const int Ceiling = 25;

        public int MaxFileMegabytes { get; set; } = 20;

        public int Megabytes => Math.Clamp(MaxFileMegabytes, 1, Ceiling);

        public long MaxBytes => Megabytes * 1024L * 1024L;
    }

    // Ok with the extension and content type the site will use, or the message the student reads
    public record FileCheck(bool Ok, string? Error, string Extension, string ContentType)
    {
        public static FileCheck Fail(string error) => new(false, error, string.Empty, string.Empty);
    }

    // What the upload really is, read from its first bytes rather than trusted from its name
    public static class SubmissionFileRules
    {
        public const string Accept = ".pdf,.docx,.zip";
        public const string WrongType = "Upload a PDF, a Word document (.docx) or a ZIP file.";

        private static readonly byte[] PdfStart = "%PDF-"u8.ToArray();
        private static readonly byte[] ZipStart = [0x50, 0x4B, 0x03, 0x04];

        private static readonly Dictionary<string, (string ContentType, string Label)> Kinds = new()
        {
            [".pdf"] = ("application/pdf", "PDF"),
            [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "Word document"),
            [".zip"] = ("application/zip", "ZIP file")
        };

        // The stream must be seekable; it is left at position 0
        public static FileCheck Check(Stream content, string? fileName, long length, long maxBytes)
        {
            if (length <= 0) return FileCheck.Fail("That file is empty.");
            if (length > maxBytes)
                return FileCheck.Fail(string.Create(CultureInfo.InvariantCulture, $"That file is too big. Files can be up to {maxBytes / (1024 * 1024)} MB."));

            var extension = Path.GetExtension(BaseName(fileName)).ToLowerInvariant();
            if (!Kinds.TryGetValue(extension, out var kind)) return FileCheck.Fail(WrongType);

            var matches = extension switch
            {
                ".pdf" => StartsWith(content, PdfStart),
                ".docx" => StartsWith(content, ZipStart) && ZipHas(content, "word/document.xml"),
                _ => StartsWith(content, ZipStart) && ZipHas(content, null)
            };
            content.Position = 0;
            return matches
                ? new FileCheck(true, null, extension, kind.ContentType)
                : FileCheck.Fail($"That file isn't a real {kind.Label}. Check that it opens on your computer, then upload it again.");
        }

        // The student's own name for the file, without folders or characters that break downloads, ending in the real extension
        public static string DisplayName(string? fileName, string extension)
        {
            var name = Path.GetFileNameWithoutExtension(BaseName(fileName));
            var clean = new StringBuilder();
            foreach (var c in name)
            {
                clean.Append(char.IsControl(c) || "\\/:*?\"<>|".Contains(c) ? '_' : c);
            }
            var result = clean.ToString().Trim().Trim('.');
            if (result.Length == 0) result = "submission";
            if (result.Length > 100) result = result[..100].TrimEnd();
            return result + extension;
        }

        // A name nobody can guess or choose: the assessment's folder and 32 random hex characters
        public static string NewStoredName(int assessmentId, string extension) =>
            string.Create(CultureInfo.InvariantCulture, $"{assessmentId}/{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}{extension}");

        public static string Icon(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "bi-file-earmark-pdf",
            ".docx" => "bi-file-earmark-word",
            ".zip" => "bi-file-earmark-zip",
            _ => "bi-file-earmark"
        };

        public static string Size(long bytes) => bytes switch
        {
            < 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes} B"),
            < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0} KB"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024):0.0} MB")
        };

        // Old browsers send the whole path ("C:\Users\me\work.pdf"); keep only the last part on any platform
        private static string BaseName(string? fileName)
        {
            var name = fileName ?? string.Empty;
            var cut = name.LastIndexOfAny(['/', '\\']);
            return cut >= 0 ? name[(cut + 1)..] : name;
        }

        private static bool StartsWith(Stream content, byte[] expected)
        {
            content.Position = 0;
            var buffer = new byte[expected.Length];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = content.Read(buffer, read, buffer.Length - read);
                if (n == 0) return false;
                read += n;
            }
            return buffer.AsSpan().SequenceEqual(expected);
        }

        // Reads only the ZIP's list of contents; nothing is unpacked
        private static bool ZipHas(Stream content, string? entry)
        {
            content.Position = 0;
            try
            {
                using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
                return entry == null ? zip.Entries.Count > 0 : zip.GetEntry(entry) != null;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }
    }

    // Assessment.LateDays: null keeps accepting work, 0 closes at the end of the due date, n allows n days late
    public static class SubmissionWindow
    {
        public static readonly IReadOnlyList<(int? Days, string Label)> Choices =
        [
            (null, "Keep accepting work after the due date (marked late)"),
            (0, "Close at the end of the due date"),
            (1, "Allow 1 day late"),
            (2, "Allow 2 days late"),
            (3, "Allow 3 days late"),
            (7, "Allow 7 days late")
        ];

        public static DateTime? LastDay(DateTime dueDate, int? lateDays) =>
            lateDays is int days ? dueDate.Date.AddDays(days) : null;

        public static bool IsOpen(DateTime dueDate, int? lateDays, DateTime today) =>
            LastDay(dueDate, lateDays) is not DateTime last || today.Date <= last;

        public static bool IsOpen(Assessment assessment, DateTime today) => IsOpen(assessment.DueDate, assessment.LateDays, today);

        // One line for the student, e.g. "Late work is accepted until Fri, 10 Oct."
        public static string Describe(DateTime dueDate, int? lateDays, DateTime today)
        {
            var last = LastDay(dueDate, lateDays);
            if (last == null) return "Work handed in after the due date is still accepted, but marked late.";
            if (today.Date > last) return $"Submissions closed at the end of {last:ddd, dd MMM yyyy}.";
            return lateDays == 0
                ? $"Submissions close at the end of the due date ({last:ddd, dd MMM})."
                : $"Late work is accepted until the end of {last:ddd, dd MMM}.";
        }

        // For the lecturer's list of assessments
        public static string Short(int? lateDays) => lateDays switch
        {
            null => "Late work accepted",
            0 => "Closes at the due date",
            1 => "1 day late allowed",
            _ => string.Create(CultureInfo.InvariantCulture, $"{lateDays} days late allowed")
        };
    }

    // Either a short-lived link to send the browser to (Blob Storage) or the file's content (local folder)
    public record FileDownload(Uri? RedirectTo, Stream? Content);

    // Where uploaded files live. Stored names are always generated by the site.
    public interface IFileStore
    {
        bool IsCloud { get; }
        string Location { get; }
        Task SaveAsync(string storedName, Stream content, string contentType, CancellationToken cancellationToken = default);
        Task<FileDownload?> OpenAsync(string storedName, string contentType, string downloadName, CancellationToken cancellationToken = default);
        Task DeleteAsync(string storedName, CancellationToken cancellationToken = default);
        Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
    }

    // Development and sites without Blob Storage: a folder outside wwwroot, so nothing is ever served without the checks
    public class LocalFileStore : IFileStore
    {
        private readonly string _root;

        public LocalFileStore(string folder)
        {
            _root = Path.GetFullPath(folder);
        }

        public bool IsCloud => false;

        public string Location => _root;

        public async Task SaveAsync(string storedName, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            var path = PathFor(storedName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await content.CopyToAsync(file, cancellationToken);
        }

        public Task<FileDownload?> OpenAsync(string storedName, string contentType, string downloadName, CancellationToken cancellationToken = default)
        {
            var path = PathFor(storedName);
            if (!File.Exists(path)) return Task.FromResult<FileDownload?>(null);
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            return Task.FromResult<FileDownload?>(new FileDownload(null, stream));
        }

        public Task DeleteAsync(string storedName, CancellationToken cancellationToken = default)
        {
            var path = PathFor(storedName);
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }

        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Directory.CreateDirectory(_root);
                return Task.FromResult(true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Task.FromResult(false);
            }
        }

        // Stored names come from the site, but never let one point outside the folder
        private string PathFor(string storedName)
        {
            var path = Path.GetFullPath(Path.Combine(_root, storedName));
            if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new ArgumentException("A stored file name must stay inside the storage folder.", nameof(storedName));
            return path;
        }
    }

    public interface ISubmissionFileService
    {
        int MaxMegabytes { get; }
        FileCheck Check(IFormFile upload);
        Task<SubmissionFile> StoreAsync(int assessmentId, IFormFile upload, FileCheck check);
        Task<SubmissionFile?> FindForStudentAsync(int fileId, int studentId);
        Task<SubmissionFile?> FindForLecturerAsync(int fileId, int lecturerId);
        Task<FileDownload?> OpenAsync(SubmissionFile file);

        // Before deleting a student, module or assessment: the files that go with it. Afterwards: remove them from storage.
        Task<List<string>> StoredNamesForStudentAsync(int studentId);
        Task<List<string>> StoredNamesForModuleAsync(int moduleId);
        Task<List<string>> StoredNamesForAssessmentAsync(int assessmentId);
        Task RemoveStoredAsync(IEnumerable<string> storedNames);
    }

    public class SubmissionFileService : ISubmissionFileService
    {
        private readonly ISubmissionFileRepository _files;
        private readonly IFileStore _store;
        private readonly TimeProvider _time;
        private readonly ILogger<SubmissionFileService> _logger;
        private readonly SubmissionFileOptions _options;

        public SubmissionFileService(
            ISubmissionFileRepository files,
            IFileStore store,
            TimeProvider time,
            ILogger<SubmissionFileService> logger,
            IOptions<SubmissionFileOptions> options)
        {
            _files = files;
            _store = store;
            _time = time;
            _logger = logger;
            _options = options.Value;
        }

        public int MaxMegabytes => _options.Megabytes;

        public FileCheck Check(IFormFile upload)
        {
            if (upload.Length > _options.MaxBytes || upload.Length <= 0)
                return SubmissionFileRules.Check(Stream.Null, upload.FileName, upload.Length, _options.MaxBytes);
            using var content = upload.OpenReadStream();
            return SubmissionFileRules.Check(content, upload.FileName, upload.Length, _options.MaxBytes);
        }

        // Puts the checked file in storage and returns its row (not saved yet); remove it again if saving fails
        public async Task<SubmissionFile> StoreAsync(int assessmentId, IFormFile upload, FileCheck check)
        {
            var storedName = SubmissionFileRules.NewStoredName(assessmentId, check.Extension);
            await using (var content = upload.OpenReadStream())
            {
                await _store.SaveAsync(storedName, content, check.ContentType);
            }
            return new SubmissionFile
            {
                FileName = SubmissionFileRules.DisplayName(upload.FileName, check.Extension),
                StoredName = storedName,
                ContentType = check.ContentType,
                SizeBytes = upload.Length,
                UploadedAt = _time.GetUtcNow().UtcDateTime
            };
        }

        public async Task<SubmissionFile?> FindForStudentAsync(int fileId, int studentId)
        {
            var file = await _files.GetWithOwnersAsync(fileId);
            return file?.Submission?.StudentId == studentId ? file : null;
        }

        public async Task<SubmissionFile?> FindForLecturerAsync(int fileId, int lecturerId)
        {
            var file = await _files.GetWithOwnersAsync(fileId);
            return file?.Submission?.Assessment?.Module?.LecturerId == lecturerId ? file : null;
        }

        public Task<FileDownload?> OpenAsync(SubmissionFile file) => _store.OpenAsync(file.StoredName, file.ContentType, file.FileName);

        public Task<List<string>> StoredNamesForStudentAsync(int studentId) => _files.GetStoredNamesForStudentAsync(studentId);

        public Task<List<string>> StoredNamesForModuleAsync(int moduleId) => _files.GetStoredNamesForModuleAsync(moduleId);

        public Task<List<string>> StoredNamesForAssessmentAsync(int assessmentId) => _files.GetStoredNamesForAssessmentAsync(assessmentId);

        // The rows are already gone, so a file that can't be removed now is only logged (it can no longer be opened)
        public async Task RemoveStoredAsync(IEnumerable<string> storedNames)
        {
            foreach (var name in storedNames)
            {
                try
                {
                    await _store.DeleteAsync(name);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogWarning(e, "Could not remove a stored submission file");
                }
            }
        }
    }
}
