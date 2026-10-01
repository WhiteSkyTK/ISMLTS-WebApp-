using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    public class ProfileViewModel
    {
        public string Role { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string? Programme { get; set; }

        // Modules a student takes or a lecturer teaches, as "CODE - Name"
        public List<string> Modules { get; set; } = new();

        public bool MustChangePassword { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public bool CanTurnOffTwoFactor { get; set; }
        public int RecoveryCodesLeft { get; set; }

        public ChangePasswordForm Password { get; set; } = new();
    }

    public class ChangePasswordForm
    {
        [Required(ErrorMessage = "Enter your current password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter a new password.")]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Type the new password again.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The two new passwords don't match.")]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    // The "scan this QR code" step, used while logging in (admins) and from the profile page (everyone)
    public class TwoFactorSetupViewModel
    {
        public string QrDataUri { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string FormController { get; set; } = string.Empty;
        public string FormAction { get; set; } = string.Empty;
        public string? ReturnUrl { get; set; }
        public string? Error { get; set; }
    }

    // Shown once, straight after the codes are made
    public class RecoveryCodesViewModel
    {
        public List<string> Codes { get; set; } = new();
        public string ContinueUrl { get; set; } = "/";
        public string ContinueText { get; set; } = "Continue";
    }

    // Shown to the admin once, straight after a reset
    public class PasswordResetViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string TemporaryPassword { get; set; } = string.Empty;
        public string BackUrl { get; set; } = "/";
    }

    // The "Sign-in and security" panel on a user's Details page (admins only)
    public class UserSecurityModel
    {
        public string Role { get; set; } = string.Empty;
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool TwoFactorEnabled { get; set; }
        public bool MustChangePassword { get; set; }
    }
}
