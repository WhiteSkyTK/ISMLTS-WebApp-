using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface IAssessmentRepository : IRepository<Assessment>
    {
        Task<IEnumerable<Assessment>> GetByModuleAsync(int moduleId);
        Task<Assessment?> GetByIdWithModuleAsync(int id);
        Task<IEnumerable<Assessment>> GetUpcomingAsync(int take);
    }
}