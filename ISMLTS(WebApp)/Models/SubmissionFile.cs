using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ISMLTS_WebApp_.Models
{
    // One uploaded file. The file itself lives in Blob Storage (or the local folder) under StoredName, which the site
    // generates; FileName is the student's own name for it, cleaned, and only ever used as the download name.
    public class SubmissionFile
    {
        [Key]
        public int SubmissionFileId { get; set; }

        [ForeignKey(nameof(Submission))]
        public int SubmissionId { get; set; }
        public Submission? Submission { get; set; }

        [Required, MaxLength(150)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string StoredName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        public DateTime UploadedAt { get; set; }
    }
}
