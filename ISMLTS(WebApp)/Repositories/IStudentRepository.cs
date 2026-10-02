using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface IStudentRepository : IRepository<Student>
    {
        Task<Student?> GetByEmailAsync(string email);
        Task<Student?> GetByCalendarTokenHashAsync(string tokenHash);
        Task<Student?> GetByIdWithModulesAsync(int id);
        Task<IEnumerable<Student>> GetByModuleAsync(int moduleId);
        Task<bool> IsEnrolledAsync(int studentId, int moduleId);
        Task<bool> EmailExistsAsync(string email, int exceptStudentId = 0);
        // programme: an exact programme name, or ListFilters.None for students without one
        Task<PagedList<Student>> SearchAsync(string? query, int page, string? sort = null, string? programme = null);
        Task<List<string>> GetProgrammesAsync();
        Task<IEnumerable<Student>> GetByIdsAsync(IEnumerable<int> ids);
        Task<List<Student>> GetAllWithModulesAsync();
    }
}