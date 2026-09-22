using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface ICourseRepository : IRepository<Course>
    {
        Task<Course?> GetByIdWithModulesAsync(int id);
    }
}