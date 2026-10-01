namespace ISMLTS_WebApp_.Models
{
    // The Release marks / Hide marks button shown on the markbook, the assessments list and the gradebook
    public class ReleaseButtonModel
    {
        public int AssessmentId { get; set; }
        public string AssessmentName { get; set; } = string.Empty;
        public bool Released { get; set; }
        public int MarkedCount { get; set; }

        // Where to come back to afterwards; the assessments list when empty
        public string? ReturnUrl { get; set; }

        public string ConfirmMessage => MarkedCount switch
        {
            0 => $"Release {AssessmentName}? Nobody has a mark yet, so each student will see their mark as soon as you save it.",
            1 => $"Release the {AssessmentName} marks? The student with a mark will see it and get a notification.",
            _ => $"Release the {AssessmentName} marks? The {MarkedCount} students with a mark will see them and get a notification."
        };
    }
}
