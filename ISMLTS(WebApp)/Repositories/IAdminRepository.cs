using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface IAdminRepository : IRepository<Admin>
    {
        Task<Admin?> GetByUsernameAsync(string username);
        Task<bool> UsernameExistsAsync(string username, int exceptAdminId = 0);
        Task<PagedList<Admin>> SearchAsync(string? query, int page);
    }
}