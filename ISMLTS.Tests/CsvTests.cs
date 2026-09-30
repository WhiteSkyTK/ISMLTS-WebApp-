using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class CsvTests
    {
        [Theory]
        [InlineData("plain", "plain")]
        [InlineData("Smith, John", "\"Smith, John\"")]
        [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
        [InlineData("line1\nline2", "\"line1\nline2\"")]
        [InlineData("=SUM(A1:A9)", "'=SUM(A1:A9)")]
        [InlineData("+27 11 000", "'+27 11 000")]
        [InlineData("-5", "'-5")]
        [InlineData("@cmd", "'@cmd")]
        [InlineData(null, "")]
        public void Field_QuotesAndDefusesFormulas(string? value, string expected) =>
            Assert.Equal(expected, Csv.Field(value));

        [Fact]
        public void Parse_ReadsQuotedFieldsCommasQuotesAndLineBreaks()
        {
            var rows = Csv.Parse("email,score,feedback\r\na@x.test,12.5,\"Good, but \"\"rushed\"\"\"\r\n\r\nb@x.test,7,\"two\nlines\"\n");

            Assert.Equal(3, rows.Count);
            Assert.Equal(new[] { "email", "score", "feedback" }, rows[0]);
            Assert.Equal(new[] { "a@x.test", "12.5", "Good, but \"rushed\"" }, rows[1]);
            Assert.Equal(new[] { "b@x.test", "7", "two\nlines" }, rows[2]);
        }

        [Fact]
        public void WriteThenParse_RoundTrips()
        {
            var original = new[] { new[] { "Name", "Note" }, new[] { "O'Brien, Sam", "said \"no\"\nthen left" } };

            var parsed = Csv.Parse(Csv.Write(original));

            Assert.Equal(original.Length, parsed.Count);
            for (var i = 0; i < original.Length; i++)
            {
                Assert.Equal(original[i], parsed[i]);
            }
        }

        [Fact]
        public void MarksExport_HasAHeaderAndOneLinePerMark()
        {
            var student = new Student { FullName = "Thandi", Email = "t@students.test" };
            var csv = Csv.MarksExport(new[]
            {
                new Mark { Student = student, AssessmentName = "POE", Score = 34.5m, MaxScore = 50, Feedback = "Clear, well argued", DateCaptured = new DateTime(2026, 9, 30) }
            });

            var rows = Csv.Parse(csv);
            Assert.Equal("Student", rows[0][0]);
            Assert.Equal(new[] { "Thandi", "t@students.test", "POE", "34.5", "50", "69", "Yes", "Clear, well argued", "2026-09-30" }, rows[1]);
        }

        [Fact]
        public void RegisterExport_ListsEveryoneEnrolled_PresentOrAbsent()
        {
            var here = new Student { StudentId = 1, FullName = "Here", Email = "here@students.test" };
            var away = new Student { StudentId = 2, FullName = "Away", Email = "away@students.test" };
            var session = new AttendanceSession
            {
                Records = { new AttendanceRecord { StudentId = 1, ScannedAt = DateTime.UtcNow, IpOnCampus = true, DistanceMeters = 12.4, Latitude = -26.1 } }
            };

            var rows = Csv.Parse(Csv.RegisterExport(session, new[] { here, away }));

            Assert.Equal(new[] { "Away", "away@students.test", "Absent", "", "", "", "", "" }, rows[1]);
            Assert.Equal(new[] { "Here", "here@students.test", "Present" }, rows[2].Take(3));
            Assert.Equal(new[] { "QR scan", "Yes", "12", "Yes" }, rows[2].Skip(4));
        }
    }
}
