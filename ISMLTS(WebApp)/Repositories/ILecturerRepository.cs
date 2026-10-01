using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface ILecturerRepository : IRepository<Lecturer>
    {
        Task<Lecturer?> GetByEmailAsync(string email);
        Task<bool> EmailExistsAsync(string email, int exceptLecturerId = 0);
        Task<PagedList<Lecturer>> SearchAsync(string? query, int page, string? sort = null);
    }
}