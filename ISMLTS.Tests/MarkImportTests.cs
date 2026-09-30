using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class MarkImportTests
    {
        private static readonly Student[] Enrolled =
        {
            new() { StudentId = 1, FullName = "Thandi", Email = "thandi@students.test" },
            new() { StudentId = 2, FullName = "Sipho", Email = "sipho@students.test" }
        };

        [Fact]
        public void Parse_MatchesStudentsByEmail_InAnyColumnOrder()
        {
            var result = MarkImport.Parse("name,score,EMAIL,feedback\r\nThandi,42.5,Thandi@Students.test,\"Good, clear\"\r\n", Enrolled, 50);

            Assert.Null(result.FileError);
            var row = Assert.Single(result.Rows);
            Assert.True(row.IsValid);
            Assert.Equal((2, 1, "Thandi", 42.5m, "Good, clear"), (row.Line, row.StudentId, row.StudentName, row.Score, row.Feedback));
        }

        [Fact]
        public void Parse_ExplainsEveryBadRow()
        {
            var csv = string.Join("\n",
                "email,score",
                ",10",
                "stranger@students.test,10",
                "sipho@students.test,",
                "sipho@students.test,abc",
                "thandi@students.test,60",
                "thandi@students.test,20");

            var errors = MarkImport.Parse(csv, Enrolled, 50).Rows.Select(r => r.Error).ToList();

            Assert.Equal(new[]
            {
                "No email address.",
                "Nobody enrolled in this module has this email.",
                "No score.",
                "This student appears more than once.",
                "The score can't be more than 50.",
                "This student appears more than once."
            }, errors);
        }

        [Fact]
        public void Parse_RejectsAFileWithoutTheRequiredColumns()
        {
            Assert.Equal("The file is empty.", MarkImport.Parse("", Enrolled, 50).FileError);
            Assert.Equal("The first row must have column names, including email and score.", MarkImport.Parse("thandi@students.test,40", Enrolled, 50).FileError);
        }

        [Fact]
        public void Parse_ReadsCommaDecimalsAsInvalid_NotAsThousands()
        {
            var row = Assert.Single(MarkImport.Parse("email,score\nthandi@students.test,\"12,5\"", Enrolled, 50).Rows);

            Assert.False(row.IsValid);
        }

        [Fact]
        public void Template_HasOneRowPerStudent_WithBlankScores()
        {
            var rows = Csv.Parse(MarkImport.Template(Enrolled));

            Assert.Equal(new[] { "email", "name", "score", "feedback" }, rows[0]);
            Assert.Equal(new[] { "sipho@students.test", "Sipho", "", "" }, rows[1]);
            Assert.Equal(3, rows.Count);
        }
    }
}
