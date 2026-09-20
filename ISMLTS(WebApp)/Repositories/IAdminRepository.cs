using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface IAdminRepository : IRepository<Admin>
    {
        Task<Admin?> GetByUsernameAsync(string username);
    }
}