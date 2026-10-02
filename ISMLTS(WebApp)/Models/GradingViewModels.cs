using Microsoft.AspNetCore.Mvc;

namespace ISMLTS_WebApp_.Models
{
    // One posted gradebook row; only these three values come from the form
    [Bind("StudentId,Score,Feedback")]
    public class GradebookEntry
    {
        public int StudentId { get; set; }
        public decimal? Score { get; set; }
        public string? Feedback { get; set; }
    }

    public class GradebookRow : GradebookEntry
    {
        public string FullName { get; set; } = string.Empty;
        public string SubmissionStatus { get; set; } = "Not Submitted";
        public string? Link { get; set; }
        public SubmissionFile? File { get; set; }
        public bool HasMark { get; set; }
    }

    public class GradebookViewModel
    {
        public int AssessmentId { get; set; }
        public int ModuleId { get; set; }
        public string AssessmentName { get; set; } = string.Empty;
        public string ModuleCode { get; set; } = string.Empty;
        public decimal OutOf { get; set; }
        public bool Released { get; set; }
        public List<GradebookRow> Rows { get; set; } = new();
    }

    public class MarkImportViewModel
    {
        public int AssessmentId { get; set; }
        public string AssessmentName { get; set; } = string.Empty;
        public string ModuleCode { get; set; } = string.Empty;
        public decimal OutOf { get; set; }
        public ISMLTS_WebApp_.Services.ImportResult? Result { get; set; }
        public string? Csv { get; set; }
    }

    // The next piece of submitted work waiting for a mark
    public class QuickEvalViewModel
    {
        public int Remaining { get; set; }
        public int Position { get; set; }
        public Submission? Current { get; set; }
        public decimal? Score { get; set; }
        public string? Feedback { get; set; }
    }
}
