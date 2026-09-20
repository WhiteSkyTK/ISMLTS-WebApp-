using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    public class Lecturer
    {
        [Key]
        public int LecturerId { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        // Navigation
        public ICollection<Module> Modules { get; set; } = new List<Module>();
    }
}
