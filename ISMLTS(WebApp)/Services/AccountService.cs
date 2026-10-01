using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    public record PasswordChangeError(string Field, string Message);

    public interface IAccountService
    {
        // Admins sign in with a username, lecturers and students with their email
        Task<UserAccount?> SignInAsync(string login, string password);
        Task<UserAccount?> FindAsync(string role, int id);
        Task SaveAsync(UserAccount account);
        Task<PasswordChangeError?> ChangePasswordAsync(UserAccount account, string currentPassword, string newPassword);
        Task ResetPasswordAsync(UserAccount account, string newPassword);
    }

    public class AccountService : IAccountService
    {
        private readonly IAdminRepository _admins;
        private readonly ILecturerRepository _lecturers;
        private readonly IStudentRepository _students;

        public AccountService(IAdminRepository admins, ILecturerRepository lecturers, IStudentRepository students)
        {
            _admins = admins;
            _lecturers = lecturers;
            _students = students;
        }

        public async Task<UserAccount?> SignInAsync(string login, string password)
        {
            // Checked in this order, and only the account whose password matches signs in
            var admin = await _admins.GetByUsernameAsync(login);
            if (admin != null && PasswordMatches(password, admin.PasswordHash)) return ForAdmin(admin);

            var lecturer = await _lecturers.GetByEmailAsync(login);
            if (lecturer != null && PasswordMatches(password, lecturer.PasswordHash)) return ForLecturer(lecturer);

            var student = await _students.GetByEmailAsync(login);
            if (student != null && PasswordMatches(password, student.PasswordHash)) return ForStudent(student);

            return null;
        }

        public async Task<UserAccount?> FindAsync(string role, int id) => role switch
        {
            Roles.Admin => await _admins.GetByIdAsync(id) is { } admin ? ForAdmin(admin) : null,
            Roles.Lecturer => await _lecturers.GetByIdAsync(id) is { } lecturer ? ForLecturer(lecturer) : null,
            Roles.Student => await _students.GetByIdAsync(id) is { } student ? ForStudent(student) : null,
            _ => null
        };

        public async Task SaveAsync(UserAccount account)
        {
            switch (account.Entity)
            {
                case Admin admin:
                    _admins.Update(admin);
                    await _admins.SaveChangesAsync();
                    break;
                case Lecturer lecturer:
                    _lecturers.Update(lecturer);
                    await _lecturers.SaveChangesAsync();
                    break;
                case Student student:
                    _students.Update(student);
                    await _students.SaveChangesAsync();
                    break;
            }
        }

        // Returns what is wrong and with which field, or null once the new password is saved
        public async Task<PasswordChangeError?> ChangePasswordAsync(UserAccount account, string currentPassword, string newPassword)
        {
            if (!PasswordMatches(currentPassword, account.Entity.PasswordHash))
                return new PasswordChangeError(nameof(ChangePasswordForm.CurrentPassword), "Your current password is not right.");
            if (!PasswordRules.IsLongEnough(newPassword))
                return new PasswordChangeError(nameof(ChangePasswordForm.NewPassword), PasswordRules.TooShortMessage);
            if (newPassword == currentPassword)
                return new PasswordChangeError(nameof(ChangePasswordForm.NewPassword), "The new password must be different from the current one.");

            await ResetPasswordAsync(account, newPassword);
            return null;
        }

        public async Task ResetPasswordAsync(UserAccount account, string newPassword)
        {
            account.Entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await SaveAsync(account);
        }

        // Accounts saved before hashing was added hold text BCrypt can't parse: treat as a failed login
        public static bool PasswordMatches(string password, string hash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or ArgumentException)
            {
                return false;
            }
        }

        private static UserAccount ForAdmin(Admin admin) => new(Roles.Admin, admin.AdminId, admin.Username, admin.Username, admin);
        private static UserAccount ForLecturer(Lecturer lecturer) => new(Roles.Lecturer, lecturer.LecturerId, lecturer.FullName, lecturer.Email, lecturer);
        private static UserAccount ForStudent(Student student) => new(Roles.Student, student.StudentId, student.FullName, student.Email, student);
    }
}
