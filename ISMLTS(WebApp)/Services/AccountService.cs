using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    public record PasswordChangeError(string Field, string Message);

    public enum TwoFactorCheck { Wrong, AppCode, RecoveryCode }

    public interface IAccountService
    {
        // Admins sign in with a username, lecturers and students with their email
        Task<UserAccount?> SignInAsync(string login, string password);
        Task<UserAccount?> FindAsync(string role, int id);
        Task SaveAsync(UserAccount account);
        Task<PasswordChangeError?> ChangePasswordAsync(UserAccount account, string currentPassword, string newPassword);
        Task ResetPasswordAsync(UserAccount account, string newPassword);

        // An admin reset: a temporary password the user must change at their next sign-in
        Task<string> ResetToTemporaryPasswordAsync(UserAccount account);

        // Admins must have an authenticator app (unless TwoFactor:RequiredForAdmins is false)
        bool MustSetUpTwoFactor(UserAccount account);
        bool CanTurnOffTwoFactor(UserAccount account);
        Task<string> StartTwoFactorSetupAsync(UserAccount account);
        Task<List<string>?> EnableTwoFactorAsync(UserAccount account, string? code);
        Task<TwoFactorCheck> CheckTwoFactorAsync(UserAccount account, string? code);
        Task<List<string>> NewRecoveryCodesAsync(UserAccount account);
        Task TurnOffTwoFactorAsync(UserAccount account);
    }

    public class AccountService : IAccountService
    {
        // On the session cookie of anyone who passed the authenticator step
        public const string TwoFactorClaim = "ismlts_2fa";

        // On the session cookie while the user still has to replace a temporary password
        public const string ChangePasswordClaim = "ismlts_change_password";

        private readonly IAdminRepository _admins;
        private readonly ILecturerRepository _lecturers;
        private readonly IStudentRepository _students;
        private readonly TwoFactorOptions _twoFactor;
        private readonly TimeProvider _clock;
        private readonly IApiRefreshTokenRepository _appSessions;

        public AccountService(IAdminRepository admins, ILecturerRepository lecturers, IStudentRepository students,
            IOptions<TwoFactorOptions> twoFactor, TimeProvider clock, IApiRefreshTokenRepository appSessions)
        {
            _appSessions = appSessions;
            _admins = admins;
            _lecturers = lecturers;
            _students = students;
            _twoFactor = twoFactor.Value;
            _clock = clock;
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
            account.Entity.MustChangePassword = false;
            await SaveAsync(account);
            await SignOutOfAppAsync(account);
        }

        public async Task<string> ResetToTemporaryPasswordAsync(UserAccount account)
        {
            var temporary = PasswordRules.NewTemporaryPassword();
            account.Entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporary);
            account.Entity.MustChangePassword = true;
            await SaveAsync(account);
            await SignOutOfAppAsync(account);
            return temporary;
        }

        public bool MustSetUpTwoFactor(UserAccount account) =>
            account.Role == Roles.Admin && _twoFactor.RequiredForAdmins && !account.Entity.TwoFactorEnabled;

        public bool CanTurnOffTwoFactor(UserAccount account) =>
            !(account.Role == Roles.Admin && _twoFactor.RequiredForAdmins);

        // Keeps the same secret until it's switched on, so refreshing the page doesn't break a QR code already scanned
        public async Task<string> StartTwoFactorSetupAsync(UserAccount account)
        {
            if (account.Entity.TwoFactorSecret == null)
            {
                account.Entity.TwoFactorSecret = TwoFactor.NewSecret();
                account.Entity.TwoFactorEnabled = false;
                await SaveAsync(account);
            }
            return account.Entity.TwoFactorSecret;
        }

        // Switches two-factor on once the app shows the right code. Returns the new recovery codes, or null if the code is wrong.
        public async Task<List<string>?> EnableTwoFactorAsync(UserAccount account, string? code)
        {
            var entity = account.Entity;
            if (entity.TwoFactorEnabled || entity.TwoFactorSecret == null) return null;
            var step = TwoFactor.Verify(entity.TwoFactorSecret, code, entity.TwoFactorLastStep, _clock.GetUtcNow().UtcDateTime);
            if (step == null) return null;

            var recoveryCodes = TwoFactor.NewRecoveryCodes();
            entity.TwoFactorEnabled = true;
            entity.TwoFactorLastStep = step.Value;
            entity.TwoFactorRecoveryCodes = TwoFactor.HashRecoveryCodes(recoveryCodes);
            await SaveAsync(account);
            return recoveryCodes;
        }

        // A code from the app, or one of the recovery codes (each works once)
        public async Task<TwoFactorCheck> CheckTwoFactorAsync(UserAccount account, string? code)
        {
            var entity = account.Entity;
            if (!entity.TwoFactorEnabled || entity.TwoFactorSecret == null) return TwoFactorCheck.Wrong;

            if (TwoFactor.LooksLikeAppCode(code))
            {
                var step = TwoFactor.Verify(entity.TwoFactorSecret, code, entity.TwoFactorLastStep, _clock.GetUtcNow().UtcDateTime);
                if (step == null) return TwoFactorCheck.Wrong;
                entity.TwoFactorLastStep = step.Value;
                await SaveAsync(account);
                return TwoFactorCheck.AppCode;
            }

            var remaining = TwoFactor.UseRecoveryCode(entity.TwoFactorRecoveryCodes, code);
            if (remaining == null) return TwoFactorCheck.Wrong;
            entity.TwoFactorRecoveryCodes = remaining;
            await SaveAsync(account);
            return TwoFactorCheck.RecoveryCode;
        }

        public async Task<List<string>> NewRecoveryCodesAsync(UserAccount account)
        {
            var codes = TwoFactor.NewRecoveryCodes();
            account.Entity.TwoFactorRecoveryCodes = TwoFactor.HashRecoveryCodes(codes);
            await SaveAsync(account);
            return codes;
        }

        // Also used by admins for someone who lost their phone
        public async Task TurnOffTwoFactorAsync(UserAccount account)
        {
            account.Entity.TwoFactorEnabled = false;
            account.Entity.TwoFactorSecret = null;
            account.Entity.TwoFactorRecoveryCodes = null;
            await SaveAsync(account);
            await SignOutOfAppAsync(account);
        }

        // A new password or a switched-off authenticator ends every Android app session for that user
        private Task SignOutOfAppAsync(UserAccount account) =>
            _appSessions.RevokeAllAsync(account.Role, account.Id, _clock.GetUtcNow().UtcDateTime);

        // The session cookie's claims; the two-factor claim says this sign-in passed the authenticator step
        public static ClaimsPrincipal Principal(UserAccount account, string scheme, bool passedTwoFactor)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, account.Id.ToString(CultureInfo.InvariantCulture)),
                new(ClaimTypes.Name, account.DisplayName),
                new(ClaimTypes.Role, account.Role)
            };
            if (passedTwoFactor) claims.Add(new Claim(TwoFactorClaim, "true"));
            if (account.Entity.MustChangePassword) claims.Add(new Claim(ChangePasswordClaim, "true"));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));
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
