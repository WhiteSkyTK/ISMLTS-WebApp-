using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface ISubmissionRepository : IRepository<Submission>
    {
        Task<IEnumerable<Submission>> GetByAssessmentAsync(int assessmentId);
        Task<Submission?> GetByAssessmentAndStudentAsync(int assessmentId, int studentId);
        Task<IEnumerable<Submission>> GetByStudentAsync(int studentId);
    }
}