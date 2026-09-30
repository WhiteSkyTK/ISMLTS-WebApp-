using System.Globalization;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Services
{
    public record ImportRow(int Line, string Email, string? StudentName, int? StudentId, decimal? Score, string? Feedback, string? Error)
    {
        public bool IsValid => Error == null;
    }

    public record ImportResult(IReadOnlyList<ImportRow> Rows, string? FileError)
    {
        public int ValidCount => Rows.Count(r => r.IsValid);
    }

    // Reads "email, score, feedback" rows (any column order, extra columns ignored) and checks each one
    public static class MarkImport
    {
        public const int MaxRows = 500;
        public const int MaxFileBytes = 1024 * 1024;

        public static ImportResult Parse(string text, IEnumerable<Student> enrolled, decimal outOf)
        {
            var table = Csv.Parse(text);
            if (table.Count == 0) return new ImportResult(Array.Empty<ImportRow>(), "The file is empty.");

            var header = table[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
            var emailColumn = header.IndexOf("email");
            var scoreColumn = header.IndexOf("score");
            var feedbackColumn = header.IndexOf("feedback");
            if (emailColumn < 0 || scoreColumn < 0)
                return new ImportResult(Array.Empty<ImportRow>(), "The first row must have column names, including email and score.");
            if (table.Count - 1 > MaxRows)
                return new ImportResult(Array.Empty<ImportRow>(), $"The file has more than {MaxRows} rows. Split it up and import each part.");

            var byEmail = enrolled
                .GroupBy(s => s.Email, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rows = new List<ImportRow>();
            for (var i = 1; i < table.Count; i++)
            {
                var cells = table[i];
                string Cell(int column) => column >= 0 && column < cells.Count ? cells[column].Trim() : string.Empty;

                var email = Cell(emailColumn);
                var scoreText = Cell(scoreColumn);
                var feedback = Cell(feedbackColumn);
                byEmail.TryGetValue(email, out var student);
                decimal? score = decimal.TryParse(scoreText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

                var error = email.Length == 0 ? "No email address."
                    : student == null ? "Nobody enrolled in this module has this email."
                    : !seen.Add(email) ? "This student appears more than once."
                    : scoreText.Length == 0 ? "No score."
                    : score == null ? "The score must be a number, like 12.5."
                    : MarkRules.CheckScore(score, outOf) ?? MarkRules.CheckFeedback(feedback);

                rows.Add(new ImportRow(i + 1, email, student?.FullName, student?.StudentId, score, feedback.Length == 0 ? null : feedback, error));
            }
            return new ImportResult(rows, null);
        }

        // A starting file: one row per enrolled student, scores left blank
        public static string Template(IEnumerable<Student> enrolled) =>
            Csv.Write(new[] { new[] { "email", "name", "score", "feedback" } }
                .Concat(enrolled.OrderBy(s => s.FullName).Select(s => new[] { s.Email, s.FullName, string.Empty, string.Empty })));
    }
}
