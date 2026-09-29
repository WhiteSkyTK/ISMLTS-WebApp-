namespace ISMLTS_WebApp_.Services
{
    public static class PasswordRules
    {
        public const int MinLength = 8;

        public static string TooShortMessage => $"Password must be at least {MinLength} characters.";

        public static bool IsLongEnough(string? password) =>
            !string.IsNullOrWhiteSpace(password) && password.Length >= MinLength;
    }
}
