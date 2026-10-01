using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    // The Jwt section. SigningKey is a secret (App Service setting Jwt__SigningKey, at least 32 characters).
    public class JwtOptions
    {
        public string? SigningKey { get; set; }
        public string Issuer { get; set; } = "ISMLTS";
        public string Audience { get; set; } = "ISMLTS.Android";
        public int AccessTokenMinutes { get; set; } = 60;
        public int RefreshTokenDays { get; set; } = 30;
    }

    // The key that signs the app's access tokens. Without a configured key a random one is made at start-up,
    // which works but signs everyone out of the app whenever the site restarts.
    public sealed class ApiSigningKey
    {
        public const int MinimumLength = 32;

        private ApiSigningKey(SymmetricSecurityKey key, bool isGenerated)
        {
            Key = key;
            IsGenerated = isGenerated;
        }

        public SymmetricSecurityKey Key { get; }
        public bool IsGenerated { get; }

        public static ApiSigningKey From(string? configured)
        {
            if (string.IsNullOrWhiteSpace(configured))
                return new ApiSigningKey(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64)), true);

            var bytes = Encoding.UTF8.GetBytes(configured);
            if (bytes.Length < MinimumLength)
                throw new InvalidOperationException($"Jwt:SigningKey must be at least {MinimumLength} characters.");
            return new ApiSigningKey(new SymmetricSecurityKey(bytes), false);
        }
    }

    public record ApiTokenPair(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

    public record ApiRefreshResult(ApiTokenPair Tokens, UserAccount Account);

    public interface IApiTokenService
    {
        Task<ApiTokenPair> IssueAsync(UserAccount account);
        Task<ApiRefreshResult?> RefreshAsync(string? refreshToken);
        Task RevokeAsync(string? refreshToken);
    }

    public class ApiTokenService : IApiTokenService
    {
        // Claim names inside the access token
        public const string SubjectClaim = "sub";
        public const string NameClaim = "name";
        public const string RoleClaim = "role";

        private readonly IApiRefreshTokenRepository _refreshTokens;
        private readonly IAccountService _accounts;
        private readonly ApiSigningKey _key;
        private readonly JwtOptions _options;
        private readonly TimeProvider _clock;

        public ApiTokenService(IApiRefreshTokenRepository refreshTokens, IAccountService accounts, ApiSigningKey key, IOptions<JwtOptions> options, TimeProvider clock)
        {
            _refreshTokens = refreshTokens;
            _accounts = accounts;
            _key = key;
            _options = options.Value;
            _clock = clock;
        }

        public async Task<ApiTokenPair> IssueAsync(UserAccount account)
        {
            var now = _clock.GetUtcNow();
            var accessExpires = now.AddMinutes(_options.AccessTokenMinutes);
            var accessToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = accessExpires.UtcDateTime,
                Claims = new Dictionary<string, object>
                {
                    [SubjectClaim] = account.Id.ToString(CultureInfo.InvariantCulture),
                    [NameClaim] = account.DisplayName,
                    [RoleClaim] = account.Role
                },
                SigningCredentials = new SigningCredentials(_key.Key, SecurityAlgorithms.HmacSha256)
            });

            var refreshToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
            var refreshExpires = now.AddDays(_options.RefreshTokenDays);
            await _refreshTokens.AddAsync(new ApiRefreshToken
            {
                Role = account.Role,
                UserId = account.Id,
                TokenHash = Hash(refreshToken),
                CreatedAt = now.UtcDateTime,
                ExpiresAt = refreshExpires.UtcDateTime
            });
            await _refreshTokens.SaveChangesAsync();

            return new ApiTokenPair(accessToken, accessExpires, refreshToken, refreshExpires);
        }

        // Swaps a refresh token for a new pair; the old one stops working. Using a token that was already swapped
        // means it may have been copied, so every app session for that user is ended.
        public async Task<ApiRefreshResult?> RefreshAsync(string? refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return null;
            var stored = await _refreshTokens.GetByHashAsync(Hash(refreshToken));
            if (stored == null) return null;

            var now = _clock.GetUtcNow().UtcDateTime;
            if (stored.RevokedAt != null)
            {
                await _refreshTokens.RevokeAllAsync(stored.Role, stored.UserId, now);
                return null;
            }
            if (stored.ExpiresAt <= now) return null;

            stored.RevokedAt = now;
            _refreshTokens.Update(stored);
            await _refreshTokens.SaveChangesAsync();

            var account = await _accounts.FindAsync(stored.Role, stored.UserId);
            if (account == null || account.Role != Roles.Student || account.Entity.MustChangePassword) return null;
            return new ApiRefreshResult(await IssueAsync(account), account);
        }

        public async Task RevokeAsync(string? refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return;
            var stored = await _refreshTokens.GetByHashAsync(Hash(refreshToken));
            if (stored == null || stored.RevokedAt != null) return;

            stored.RevokedAt = _clock.GetUtcNow().UtcDateTime;
            _refreshTokens.Update(stored);
            await _refreshTokens.SaveChangesAsync();
        }

        // 256 random bits, so a fast hash is enough
        public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
