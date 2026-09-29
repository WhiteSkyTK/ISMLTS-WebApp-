using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface IMarkRepository : IRepository<Mark>
    {
        Task<IEnumerable<Mark>> GetByModuleAsync(int moduleId);
        Task<IEnumerable<Mark>> GetByModulesAsync(IReadOnlyCollection<int> moduleIds);
        Task<IEnumerable<Mark>> GetByStudentAsync(int studentId);
        Task<Mark?> GetByIdWithDetailsAsync(int id);
        Task<List<Mark>> GetByAssessmentAsync(int assessmentId);
        Task<Mark?> GetByAssessmentAndStudentAsync(int assessmentId, int studentId);
        Task<HashSet<(int AssessmentId, int StudentId)>> GetMarkedPairsAsync(IReadOnlyCollection<int> assessmentIds);
        Task UnlinkAssessmentAsync(int assessmentId);
    }
}
