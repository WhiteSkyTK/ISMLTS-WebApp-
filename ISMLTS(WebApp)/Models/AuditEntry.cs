using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    public static class AuditActions
    {
        public const string AccountCreated = "Account created";
        public const string AccountDeleted = "Account deleted";
        public const string AccountsImported = "Accounts imported";
        public const string PasswordReset = "Password reset";
        public const string TwoFactorOff = "Two-factor turned off";
        public const string EnrolmentChanged = "Enrolment changed";

        public static readonly IReadOnlyList<string> All =
            [AccountCreated, AccountDeleted, AccountsImported, PasswordReset, TwoFactorOff, EnrolmentChanged];
    }

    // Who did what to whom: one row per admin action that changes accounts or enrolments. Never holds passwords.
    public class AuditEntry
    {
        [Key]
        public int AuditEntryId { get; set; }

        public DateTime At { get; set; }

        [Required, MaxLength(10)]
        public string ActorRole { get; set; } = string.Empty;

        public int ActorId { get; set; }

        [Required, MaxLength(100)]
        public string ActorName { get; set; } = string.Empty;

        [Required, MaxLength(40)]
        public string Action { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Target { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Details { get; set; }
    }
}
