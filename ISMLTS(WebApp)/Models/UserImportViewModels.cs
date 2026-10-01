using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Models
{
    public class UserImportViewModel
    {
        // "students" or "lecturers"
        public string Kind { get; set; } = "students";
        public string? Csv { get; set; }
        public UserImportResult? Result { get; set; }

        public bool IsStudents => Kind == "students";
        public string Noun => IsStudents ? "students" : "lecturers";
    }

    public class UserImportDoneViewModel
    {
        public string Kind { get; set; } = "students";
        public List<ImportedAccount> Accounts { get; set; } = new();
        public int Skipped { get; set; }
        public string? CourseCode { get; set; }

        // The new accounts as a CSV the admin can save once (name, email, temporary password)
        public string CredentialsCsv => Csv.Write(new[] { new[] { "name", "email", "temporary password" } }
            .Concat(Accounts.Select(a => new[] { a.Name, a.Email, a.TemporaryPassword })));
    }
}
