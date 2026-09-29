using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface IStudentRepository : IRepository<Student>
    {
        Task<Student?> GetByEmailAsync(string email);
        Task<Student?> GetByIdWithModulesAsync(int id);
        Task<IEnumerable<Student>> GetByModuleAsync(int moduleId);
        Task<bool> IsEnrolledAsync(int studentId, int moduleId);
        Task<bool> EmailExistsAsync(string email, int exceptStudentId = 0);
        Task<PagedList<Student>> SearchAsync(string? query, int page);
    }
}