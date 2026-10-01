namespace ISMLTS_WebApp_.Models
{
    // What signing in and the profile page need from Admin, Lecturer and Student (users live in three tables).
    // None of these are ever bound from a form.
    public interface IUserAccount
    {
        string PasswordHash { get; set; }

        // Authenticator app sign-in: the shared secret (Base32), whether it's switched on, the last 30-second
        // step a code was accepted for (so one code can't be used twice) and hashes of the unused recovery codes
        string? TwoFactorSecret { get; set; }
        bool TwoFactorEnabled { get; set; }
        long TwoFactorLastStep { get; set; }
        string? TwoFactorRecoveryCodes { get; set; }
    }

    // One signed-in user, whichever table they live in. Login is the admin username or the lecturer/student email.
    public record UserAccount(string Role, int Id, string DisplayName, string Login, IUserAccount Entity);
}
