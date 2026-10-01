using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    public class Lecturer : IUserAccount
    {
        [Key]
        public int LecturerId { get; set; }

        [Required, MaxLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        // No [Required]: set from a hashed password field, never bound from a form
        public string PasswordHash { get; set; } = string.Empty;

        // Navigation
        public ICollection<Module> Modules { get; set; } = new List<Module>();
    }
}
