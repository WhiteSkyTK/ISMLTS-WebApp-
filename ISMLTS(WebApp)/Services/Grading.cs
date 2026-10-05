using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Services
{
    public static class Grading
    {
        // The error key for a problem with the whole form rather than one row
        public const int WholeForm = -1;

        // Per-row problems for a gradebook save, keyed by row index. Blank scores are allowed (that row is skipped).
        public static Dictionary<int, string> ValidateGradebook(IReadOnlyList<GradebookEntry> rows, IReadOnlySet<int> enrolledStudentIds, decimal outOf)
        {
            var errors = new Dictionary<int, string>();
            var seen = new HashSet<int>();
            // The form has one row per enrolled student; a longer list didn't come from it, so it is refused before any looping
            if (rows.Count > enrolledStudentIds.Count)
            {
                errors[WholeForm] = "There are more rows than students in this module. Reload the gradebook and try again.";
                return errors;
            }
            var count = Math.Min(rows.Count, enrolledStudentIds.Count);
            for (var i = 0; i < count; i++)
            {
                var row = rows[i];
                var error = !enrolledStudentIds.Contains(row.StudentId) ? "This student isn't enrolled in the module."
                    : !seen.Add(row.StudentId) ? "This student appears twice."
                    : row.Score == null ? (string.IsNullOrWhiteSpace(row.Feedback) ? null : "Enter a score to save feedback.")
                    : MarkRules.CheckScore(row.Score, outOf) ?? MarkRules.CheckFeedback(row.Feedback);
                if (error != null) errors[i] = error;
            }
            return errors;
        }

        // Quick Eval queue: submitted work with no mark yet, oldest due date first, then earliest submitted
        public static List<Submission> QuickEvalQueue(IEnumerable<Submission> submissions, IReadOnlySet<(int AssessmentId, int StudentId)> marked) =>
            submissions
                .Where(s => s.SubmittedAt != null && s.Assessment != null && !marked.Contains((s.AssessmentId, s.StudentId)))
                .OrderBy(s => s.Assessment!.DueDate)
                .ThenBy(s => s.SubmittedAt)
                .ThenBy(s => s.SubmissionId)
                .ToList();
    }
}
