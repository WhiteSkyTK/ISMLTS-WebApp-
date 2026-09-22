using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface IMarkRepository : IRepository<Mark>
    {
        Task<IEnumerable<Mark>> GetByModuleAsync(int moduleId);
        Task<IEnumerable<Mark>> GetByStudentAsync(int studentId);
        Task<Mark?> GetByIdWithDetailsAsync(int id);
    }
}