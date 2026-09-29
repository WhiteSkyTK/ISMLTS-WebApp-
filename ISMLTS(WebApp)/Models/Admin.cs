using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    public class Admin
    {
        [Key]
        public int AdminId { get; set; }

        [Required, MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        // No [Required]: set from a hashed password field, never bound from a form
        public string PasswordHash { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Role { get; set; } = "Admin";
    }
}
