using System.ComponentModel.DataAnnotations;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    public record UserImportRow(int Line, string Name, string Email, string? Programme, string? Error)
    {
        public bool IsValid => Error == null;
    }

    public record UserImportResult(IReadOnlyList<UserImportRow> Rows, string? FileError)
    {
        public int ValidCount => Rows.Count(r => r.IsValid);
    }

    public record ImportedAccount(string Name, string Email, string TemporaryPassword);

    // Reads "name, email[, programme]" rows for new students or lecturers and checks each one. Nothing is saved here.
    public static class UserImport
    {
        public const int MaxRows = 300;
        public const int MaxFileBytes = 1024 * 1024;
        private static readonly EmailAddressAttribute EmailCheck = new();

        public static UserImportResult Parse(string text, bool students, IReadOnlyCollection<string> emailsInUse, StudentOptions studentOptions, IReadOnlyCollection<string> programmes)
        {
            var table = Csv.Parse(text).Where(row => row.Exists(cell => cell.Trim().Length > 0)).ToList();
            if (table.Count == 0) return new UserImportResult(Array.Empty<UserImportRow>(), "The file is empty.");

            var header = table[0].Select(h => h.Trim().ToLowerInvariant().Replace(" ", "")).ToList();
            var nameColumn = header.FindIndex(h => h is "name" or "fullname");
            var emailColumn = header.IndexOf("email");
            var programmeColumn = students ? header.IndexOf("programme") : -1;
            if (nameColumn < 0 || emailColumn < 0)
                return new UserImportResult(Array.Empty<UserImportRow>(), "The first row must have column names, including name and email.");
            if (table.Count - 1 > MaxRows)
                return new UserImportResult(Array.Empty<UserImportRow>(), $"The file has more than {MaxRows} people. Split it up and import each part.");

            var inUse = new HashSet<string>(emailsInUse, StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rows = new List<UserImportRow>();
            for (var i = 1; i < table.Count; i++)
            {
                var cells = table[i];
                string Cell(int column) => column >= 0 && column < cells.Count ? cells[column].Trim() : string.Empty;

                var name = Cell(nameColumn);
                var email = Cell(emailColumn);
                var programme = MatchProgramme(Cell(programmeColumn), programmes);
                var error = CheckName(name)
                    ?? CheckEmail(email, students, studentOptions, inUse, seen)
                    ?? CheckProgramme(Cell(programmeColumn), programme);
                rows.Add(new UserImportRow(i + 1, name, email, programme, error));
            }
            return new UserImportResult(rows, null);
        }

        // A starting file with the right column names and one example row
        public static string Template(bool students, StudentOptions studentOptions) => students
            ? Csv.Write(new[] { new[] { "name", "email", "programme" }, new[] { "Thandi Mokoena", $"st10000001@{studentOptions.EmailDomain}", "Advanced Diploma in Application Development" } })
            : Csv.Write(new[] { new[] { "name", "email" }, new[] { "Nomsa Dlamini", "nomsa.dlamini@rosebank.iie.ac.za" } });

        private static string? CheckName(string name)
        {
            if (name.Length == 0) return "No name.";
            return name.Length > 100 ? "The name is longer than 100 characters." : null;
        }

        private static string? CheckEmail(string email, bool students, StudentOptions studentOptions, HashSet<string> inUse, HashSet<string> seen)
        {
            if (email.Length == 0) return "No email address.";
            if (email.Length > 150 || !EmailCheck.IsValid(email)) return "This is not an email address.";
            if (students && !studentOptions.IsStudentEmail(email)) return studentOptions.EmailDomainMessage;
            if (inUse.Contains(email)) return "That email already belongs to a student or lecturer.";
            return seen.Add(email) ? null : "This email appears more than once in the file.";
        }

        private static string? CheckProgramme(string typed, string? matched) =>
            typed.Length > 0 && matched == null ? "Unknown programme. Use the name of a course, or leave it blank." : null;

        // Programmes are course names; matching ignores case so "advanced diploma…" still works
        private static string? MatchProgramme(string typed, IReadOnlyCollection<string> programmes) =>
            typed.Length == 0 ? null : programmes.FirstOrDefault(p => string.Equals(p, typed, StringComparison.OrdinalIgnoreCase));
    }

    public interface IUserImportService
    {
        Task<UserImportResult> CheckAsync(string csv, bool students);
        Task<List<ImportedAccount>> ImportAsync(UserImportResult result, bool students, Course? enrolIn, string term);
    }

    // Creates the accounts from a checked file. Everyone gets a temporary password they must change at first log-in.
    public class UserImportService : IUserImportService
    {
        private readonly IStudentRepository _students;
        private readonly ILecturerRepository _lecturers;
        private readonly ICourseRepository _courses;
        private readonly ICourseService _courseService;
        private readonly StudentOptions _studentOptions;

        public UserImportService(IStudentRepository students, ILecturerRepository lecturers, ICourseRepository courses,
            ICourseService courseService, Microsoft.Extensions.Options.IOptions<StudentOptions> studentOptions)
        {
            _students = students;
            _lecturers = lecturers;
            _courses = courses;
            _courseService = courseService;
            _studentOptions = studentOptions.Value;
        }

        // Always checked against the database as it is now, so a file can't be trusted from an earlier preview
        public async Task<UserImportResult> CheckAsync(string csv, bool students)
        {
            var emailsInUse = (await _students.GetAllAsync()).Select(s => s.Email)
                .Concat((await _lecturers.GetAllAsync()).Select(l => l.Email)).ToList();
            var programmes = (await _courses.GetAllAsync()).Select(c => c.Name).ToList();
            return UserImport.Parse(csv, students, emailsInUse, _studentOptions, programmes);
        }

        public async Task<List<ImportedAccount>> ImportAsync(UserImportResult result, bool students, Course? enrolIn, string term)
        {
            var valid = result.Rows.Where(r => r.IsValid).ToList();
            var passwords = valid.Select(_ => PasswordRules.NewTemporaryPassword()).ToList();
            // Hashing is deliberately slow; spread it over the cores so a class list doesn't time out
            var hashes = passwords.AsParallel().AsOrdered().Select(p => BCrypt.Net.BCrypt.HashPassword(p)).ToList();

            var created = new List<Student>();
            for (var i = 0; i < valid.Count; i++)
            {
                var row = valid[i];
                if (students)
                {
                    var student = new Student { FullName = row.Name, Email = row.Email, Programme = row.Programme, PasswordHash = hashes[i], MustChangePassword = true };
                    await _students.AddAsync(student);
                    created.Add(student);
                }
                else
                {
                    await _lecturers.AddAsync(new Lecturer { FullName = row.Name, Email = row.Email, PasswordHash = hashes[i], MustChangePassword = true });
                }
            }
            if (students) await _students.SaveChangesAsync();
            else await _lecturers.SaveChangesAsync();

            if (students && enrolIn != null && created.Count > 0)
            {
                await _courseService.EnrolAsync(enrolIn, created.Select(s => s.StudentId).ToList(), term);
            }
            return valid.Select((row, i) => new ImportedAccount(row.Name, row.Email, passwords[i])).ToList();
        }
    }
}
