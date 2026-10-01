using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface IApiRefreshTokenRepository : IRepository<ApiRefreshToken>
    {
        Task<ApiRefreshToken?> GetByHashAsync(string tokenHash);

        // Signs the user out of the app everywhere (password reset, two-factor switched off, a stolen token used)
        Task RevokeAllAsync(string role, int userId, DateTime now);
    }
}
