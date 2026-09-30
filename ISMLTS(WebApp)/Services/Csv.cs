using System.Globalization;
using System.Text;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Services
{
    public static class Csv
    {
        private static readonly char[] NeedsQuotes = { ',', '"', '\r', '\n' };

        // A cell starting with one of these would run as a formula in Excel (CSV injection), so it gets a leading '
        private static readonly char[] FormulaStarts = { '=', '+', '-', '@', '\t', '\r' };

        public static string Field(string? value)
        {
            var text = value ?? string.Empty;
            if (text.Length > 0 && FormulaStarts.Contains(text[0])) text = "'" + text;
            return text.IndexOfAny(NeedsQuotes) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
        }

        public static string Write(IEnumerable<IEnumerable<string?>> rows)
        {
            var csv = new StringBuilder();
            foreach (var row in rows)
            {
                csv.Append(string.Join(',', row.Select(Field))).Append("\r\n");
            }
            return csv.ToString();
        }

        // With a byte-order mark so Excel opens accented names correctly
        public static byte[] ToUtf8WithBom(string csv) => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();

        // RFC 4180: quoted fields may hold commas, doubled quotes and line breaks. Blank lines are skipped.
        public static List<List<string>> Parse(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            var quoted = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (quoted)
                {
                    if (c != '"') field.Append(c);
                    else if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = false;
                    continue;
                }

                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        row.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        EndRow();
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }
            EndRow();
            return rows;

            void EndRow()
            {
                row.Add(field.ToString());
                field.Clear();
                if (row.Count > 1 || row[0].Trim().Length > 0) rows.Add(row);
                row = new List<string>();
            }
        }

        private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        // ---------- The two exports ----------

        public static string MarksExport(IEnumerable<Mark> marks)
        {
            var rows = new List<string?[]>
            {
                new[] { "Student", "Email", "Assessment", "Score", "Out of", "Percent", "Released", "Feedback", "Date captured" }
            };
            rows.AddRange(marks
                .OrderBy(m => m.Student?.FullName)
                .ThenBy(m => m.DateCaptured)
                .Select(m => new[]
                {
                    m.Student?.FullName, m.Student?.Email, m.AssessmentName,
                    Number(m.Score), Number(m.MaxScore), Number(m.Percentage),
                    m.IsVisibleToStudent ? "Yes" : "No", m.Feedback,
                    m.DateCaptured.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                }));
            return Write(rows);
        }

        // Everyone enrolled, present or absent, with how their attendance was checked
        public static string RegisterExport(AttendanceSession session, IEnumerable<Student> enrolled)
        {
            var records = session.Records.GroupBy(r => r.StudentId).ToDictionary(g => g.Key, g => g.First());
            var rows = new List<string?[]>
            {
                new[] { "Student", "Email", "Status", "Time", "How", "On campus network", "Distance (m)", "Location shared" }
            };
            rows.AddRange(enrolled.OrderBy(s => s.FullName).Select(s =>
            {
                if (!records.TryGetValue(s.StudentId, out var r))
                    return new[] { s.FullName, s.Email, "Absent", null, null, null, null, null };

                return new[]
                {
                    s.FullName, s.Email, "Present",
                    r.ScannedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    r.IsManual ? "Marked by lecturer" : "QR scan",
                    r.IsManual ? null : (r.IpOnCampus ? "Yes" : "No"),
                    r.DistanceMeters?.ToString("0", CultureInfo.InvariantCulture),
                    r.IsManual ? null : (r.Latitude != null ? "Yes" : "No")
                };
            }));
            return Write(rows);
        }
    }
}
