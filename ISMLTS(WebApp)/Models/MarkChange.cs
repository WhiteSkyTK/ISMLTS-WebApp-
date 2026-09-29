using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    public static class MarkChangeActions
    {
        public const string Created = "Created";
        public const string Updated = "Updated";
        public const string Deleted = "Deleted";
    }

    public static class MarkSources
    {
        public const string Form = "Mark form";
        public const string Gradebook = "Gradebook";
        public const string QuickEval = "Quick Eval";
        public const string Import = "CSV import";
    }

    // Audit trail of every change to a mark: who, when, and old → new. No foreign key to Mark, so the
    // history survives when a mark is deleted.
    public class MarkChange
    {
        [Key]
        public int MarkChangeId { get; set; }

        public int MarkId { get; set; }
        public int StudentId { get; set; }
        public int ModuleId { get; set; }

        [Required, MaxLength(100)]
        public string AssessmentName { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Action { get; set; } = MarkChangeActions.Created;

        public decimal? OldScore { get; set; }
        public decimal? NewScore { get; set; }
        public decimal? OldMaxScore { get; set; }
        public decimal? NewMaxScore { get; set; }
        public bool FeedbackChanged { get; set; }

        [Required, MaxLength(20)]
        public string Source { get; set; } = MarkSources.Form;

        public int ChangedById { get; set; }

        [Required, MaxLength(100)]
        public string ChangedByName { get; set; } = string.Empty;

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    }
}
