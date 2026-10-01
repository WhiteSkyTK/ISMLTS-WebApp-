using System.Security.Cryptography;

namespace ISMLTS_WebApp_.Services
{
    public static class PasswordRules
    {
        public const int MinLength = 8;
        private const string TemporaryAlphabet = "abcdefghjkmnpqrstuvwxyz23456789";

        public static string TooShortMessage => $"Password must be at least {MinLength} characters.";

        public static bool IsLongEnough(string? password) =>
            !string.IsNullOrWhiteSpace(password) && password.Length >= MinLength;

        // What an admin reads out or writes down after a reset: 12 random characters in groups, no look-alikes (l/1, o/0)
        public static string NewTemporaryPassword()
        {
            var letters = new char[12];
            for (var i = 0; i < letters.Length; i++)
            {
                letters[i] = TemporaryAlphabet[RandomNumberGenerator.GetInt32(TemporaryAlphabet.Length)];
            }
            return $"{new string(letters, 0, 4)}-{new string(letters, 4, 4)}-{new string(letters, 8, 4)}";
        }
    }
}
