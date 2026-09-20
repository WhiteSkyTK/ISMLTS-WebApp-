using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface ILecturerRepository : IRepository<Lecturer>
    {
        Task<Lecturer?> GetByEmailAsync(string email);
    }
}