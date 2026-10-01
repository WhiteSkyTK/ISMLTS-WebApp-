using System.ComponentModel.DataAnnotations;

namespace ISMLTS_WebApp_.Models
{
    // Lets the Android app get a new access token without the password. Each one works once (it is replaced on use)
    // and only its SHA-256 hash is stored, so a copy of the database can't be used to sign in.
    public class ApiRefreshToken
    {
        [Key]
        public int ApiRefreshTokenId { get; set; }

        [Required, MaxLength(20)]
        public string Role { get; set; } = string.Empty;

        public int UserId { get; set; }

        [Required, MaxLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
    }
}
