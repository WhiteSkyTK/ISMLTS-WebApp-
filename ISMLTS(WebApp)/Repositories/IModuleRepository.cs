using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface IModuleRepository : IRepository<Module>
    {
        Task<Module?> GetByCodeAsync(string code);
        Task<bool> CodeExistsAsync(string code, int exceptModuleId = 0);
        Task<IEnumerable<Module>> GetByLecturerAsync(int lecturerId);
        Task<PagedList<Module>> SearchAsync(string? query, int page);
        Task<Module?> GetByIdWithDetailsAsync(int id);
    }
}