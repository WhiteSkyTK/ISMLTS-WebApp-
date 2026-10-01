using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class UserImportTests
    {
        private static readonly StudentOptions College = new();
        private static readonly string[] Programmes = { "Advanced Diploma in Application Development", "Diploma in Software Development" };

        private static UserImportResult Students(string csv, params string[] inUse) =>
            UserImport.Parse(csv, students: true, inUse, College, Programmes);

        [Fact]
        public void GoodRows_AreReady_AndProgrammesMatchWhateverTheCase()
        {
            var result = Students("name,email,programme\nThandi Mokoena,st1@rcconnect.edu.za,diploma in software development\nSipho Ndlovu,st2@rcconnect.edu.za,\n");

            Assert.Null(result.FileError);
            Assert.Equal(2, result.ValidCount);
            Assert.Equal("Diploma in Software Development", result.Rows[0].Programme);
            Assert.Null(result.Rows[1].Programme);
            Assert.Equal(2, result.Rows[0].Line);
        }

        [Theory]
        [InlineData(",st1@rcconnect.edu.za,", "No name.")]
        [InlineData("Thandi,,", "No email address.")]
        [InlineData("Thandi,not-an-email,", "This is not an email address.")]
        [InlineData("Thandi,thandi@gmail.com,", "Student emails must end in @rcconnect.edu.za.")]
        [InlineData("Thandi,taken@rcconnect.edu.za,", "That email already belongs to a student or lecturer.")]
        [InlineData("Thandi,st1@rcconnect.edu.za,Bachelor of Magic", "Unknown programme. Use the name of a course, or leave it blank.")]
        public void BadRows_SayWhatIsWrong(string row, string error)
        {
            var result = Students($"name,email,programme\n{row}\n", "TAKEN@rcconnect.edu.za");

            Assert.Equal(error, Assert.Single(result.Rows).Error);
        }

        [Fact]
        public void TheSameEmailTwiceInTheFile_IsOnlyAddedOnce()
        {
            var result = Students("name,email\nA,st1@rcconnect.edu.za\nB,ST1@rcconnect.edu.za\n");

            Assert.True(result.Rows[0].IsValid);
            Assert.Equal("This email appears more than once in the file.", result.Rows[1].Error);
        }

        [Fact]
        public void ColumnsCanBeInAnyOrder_AndFullNameWorks_AndBlankLinesAreSkipped()
        {
            var result = Students("Email,Full Name,Extra\n\nst1@rcconnect.edu.za,Thandi Mokoena,x\n,,\n");

            var row = Assert.Single(result.Rows);
            Assert.Equal(("Thandi Mokoena", "st1@rcconnect.edu.za"), (row.Name, row.Email));
        }

        [Theory]
        [InlineData("")]
        [InlineData("email\nst1@rcconnect.edu.za\n")]
        [InlineData("name,mail\nA,b\n")]
        public void FilesWithoutTheRightColumns_AreRejected(string csv)
        {
            Assert.NotNull(Students(csv).FileError);
        }

        [Fact]
        public void TooManyRows_AreRejected()
        {
            var rows = string.Join('\n', Enumerable.Range(0, UserImport.MaxRows + 1).Select(i => $"S{i},st{i}@rcconnect.edu.za"));

            Assert.Contains("more than", Students($"name,email\n{rows}").FileError);
        }

        [Fact]
        public void Lecturers_CanUseAnyEmailDomain_AndHaveNoProgramme()
        {
            var result = UserImport.Parse("name,email,programme\nNomsa Dlamini,nomsa@rosebank.iie.ac.za,Whatever\n", students: false, Array.Empty<string>(), College, Programmes);

            var row = Assert.Single(result.Rows);
            Assert.True(row.IsValid);
            Assert.Null(row.Programme);
        }

        [Fact]
        public void Templates_HaveTheRightColumns()
        {
            Assert.StartsWith("name,email,programme", UserImport.Template(true, College));
            Assert.StartsWith("name,email\r\n", UserImport.Template(false, College));
        }

        [Fact]
        public void TemporaryPasswords_AreLongRandomAndReadable()
        {
            var passwords = Enumerable.Range(0, 50).Select(_ => PasswordRules.NewTemporaryPassword()).ToList();

            Assert.Equal(50, passwords.Distinct().Count());
            Assert.All(passwords, p => Assert.Matches("^[a-km-np-z2-9]{4}-[a-km-np-z2-9]{4}-[a-km-np-z2-9]{4}$", p));
            Assert.All(passwords, p => Assert.True(PasswordRules.IsLongEnough(p)));
        }
    }
}
