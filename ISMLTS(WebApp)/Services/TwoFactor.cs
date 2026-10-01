using System.Security.Cryptography;
using System.Text;
using OtpNet;

namespace ISMLTS_WebApp_.Services
{
    public class TwoFactorOptions
    {
        // Admins must set up an authenticator app before they can use the site
        public bool RequiredForAdmins { get; set; } = true;
    }

    // Authenticator app codes (TOTP, RFC 6238: 6 digits, 30 seconds) and one-time recovery codes
    public static class TwoFactor
    {
        public const string Issuer = "ISMLTS";
        public const int RecoveryCodeCount = 8;
        private const string RecoveryAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        // 160 random bits, the size authenticator apps expect
        public static string NewSecret() => Base32Encoding.ToString(RandomNumberGenerator.GetBytes(20));

        // What the QR code holds; authenticator apps show it as "ISMLTS (login)"
        public static string OtpAuthUri(string secret, string login) =>
            $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(login)}?secret={secret}&issuer={Uri.EscapeDataString(Issuer)}&digits=6&period=30";

        // The secret in groups of four, for typing into an app by hand
        public static string Grouped(string secret) =>
            string.Join(' ', secret.Chunk(4).Select(chunk => new string(chunk)));

        public static bool LooksLikeAppCode(string? code)
        {
            var digits = Clean(code);
            return digits.Length == 6 && digits.All(char.IsAsciiDigit);
        }

        // Accepts the code for now or 30 seconds either side (clock drift). Returns the time step it matched,
        // or null when it's wrong or that step was already used.
        public static long? Verify(string secret, string? code, long lastUsedStep, DateTime utcNow)
        {
            if (!LooksLikeAppCode(code)) return null;
            var totp = new Totp(Base32Encoding.ToBytes(secret));
            return totp.VerifyTotp(utcNow, Clean(code), out var step, VerificationWindow.RfcSpecifiedNetworkDelay) && step > lastUsedStep
                ? step
                : null;
        }

        // Shown once, stored only as hashes. Format ABCD-EFGH.
        public static List<string> NewRecoveryCodes() =>
            Enumerable.Range(0, RecoveryCodeCount).Select(_ => NewRecoveryCode()).ToList();

        private static string NewRecoveryCode()
        {
            var letters = new char[8];
            for (var i = 0; i < letters.Length; i++)
            {
                letters[i] = RecoveryAlphabet[RandomNumberGenerator.GetInt32(RecoveryAlphabet.Length)];
            }
            return $"{new string(letters, 0, 4)}-{new string(letters, 4, 4)}";
        }

        public static string HashRecoveryCodes(IEnumerable<string> codes) => string.Join(';', codes.Select(Hash));

        public static int RecoveryCodesLeft(string? stored) =>
            string.IsNullOrEmpty(stored) ? 0 : stored.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;

        // Removes the matching code. Returns the codes still unused, or null when the code isn't one of them.
        public static string? UseRecoveryCode(string? stored, string? code)
        {
            if (string.IsNullOrEmpty(stored) || string.IsNullOrWhiteSpace(code)) return null;
            var hashes = stored.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
            var wanted = Convert.FromHexString(Hash(code));
            var match = hashes.FindIndex(h => CryptographicOperations.FixedTimeEquals(Convert.FromHexString(h), wanted));
            if (match < 0) return null;
            hashes.RemoveAt(match);
            return string.Join(';', hashes);
        }

        private static string Clean(string? code) =>
            new((code ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());

        // Recovery codes are long and random, so a plain SHA-256 is enough to keep them unreadable in the database
        private static string Hash(string code) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Clean(code).ToUpperInvariant())));
    }
}
