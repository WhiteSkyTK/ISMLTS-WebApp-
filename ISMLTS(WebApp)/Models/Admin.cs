using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    public class Admin : IUserAccount
    {
        [Key]
        public int AdminId { get; set; }

        [Required, MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        // No [Required]: set from a hashed password field, never bound from a form
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(64)]
        public string? TwoFactorSecret { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public long TwoFactorLastStep { get; set; }

        [MaxLength(600)]
        public string? TwoFactorRecoveryCodes { get; set; }
        public bool MustChangePassword { get; set; }

        [Required, MaxLength(50)]
        public string Role { get; set; } = "Admin";
    }
}
