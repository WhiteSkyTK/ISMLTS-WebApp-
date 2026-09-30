namespace ISMLTS_WebApp_.Models
{
    // One assessment column in a module's markbook
    public class MarkbookColumn
    {
        public int AssessmentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public decimal MaxScore { get; set; }
        public bool MarksReleased { get; set; }
        public int MarkedCount { get; set; }
        public int ToMarkCount { get; set; }
        public decimal? AveragePercentage { get; set; }
    }

    public class MarkbookCell
    {
        public int AssessmentId { get; set; }
        public Mark? Mark { get; set; }

        // Submitted, Late or Not Submitted
        public string SubmissionStatus { get; set; } = "Not Submitted";

        public bool IsWaitingForMark => Mark == null && SubmissionStatus != "Not Submitted";
    }

    public static class MarkbookStatuses
    {
        public const string AtRisk = "at-risk";
        public const string OnTrack = "on-track";
        public const string NoMarks = "no-marks";
        public const string ToMark = "to-mark";
    }

    public class MarkbookRow
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        // Same order as the markbook's columns
        public List<MarkbookCell> Cells { get; set; } = new();

        // Marks not linked to an assessment (captured under "Other")
        public List<Mark> OtherMarks { get; set; } = new();

        public decimal? AveragePercentage { get; set; }
        public bool IsAtRisk { get; set; }
        public bool HasMarks => AveragePercentage != null;
        public int ToMarkCount => Cells.Count(c => c.IsWaitingForMark);

        // Space-separated tokens the "Show" filter matches on
        public string StatusTokens
        {
            get
            {
                var status = !HasMarks ? MarkbookStatuses.NoMarks : IsAtRisk ? MarkbookStatuses.AtRisk : MarkbookStatuses.OnTrack;
                return ToMarkCount > 0 ? $"{status} {MarkbookStatuses.ToMark}" : status;
            }
        }
    }

    public class MarkbookViewModel
    {
        public int ModuleId { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleDisplay { get; set; } = string.Empty;
        public List<MarkbookColumn> Columns { get; set; } = new();
        public List<MarkbookRow> Rows { get; set; } = new();

        public int AtRiskCount => Rows.Count(r => r.HasMarks && r.IsAtRisk);
        public int ToMarkCount => Rows.Sum(r => r.ToMarkCount);

        // Marks students can't see yet because their assessment isn't released
        public int HiddenMarkCount => Columns.Where(c => !c.MarksReleased).Sum(c => c.MarkedCount);
    }
}
