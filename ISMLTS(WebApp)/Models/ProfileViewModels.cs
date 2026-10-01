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
}
