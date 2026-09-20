using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface IStudentRepository : IRepository<Student>
    {
        Task<Student?> GetByEmailAsync(string email);
        Task<IEnumerable<Student>> GetByModuleAsync(int moduleId);
    }
}