using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface ISubmissionFileRepository : IRepository<SubmissionFile>
    {
        // The file with its submission, assessment and module, so callers can check who may open it
        Task<SubmissionFile?> GetWithOwnersAsync(int fileId);

        // Stored names of every file that goes when a student, module or assessment is deleted
        Task<List<string>> GetStoredNamesForStudentAsync(int studentId);
        Task<List<string>> GetStoredNamesForModuleAsync(int moduleId);
        Task<List<string>> GetStoredNamesForAssessmentAsync(int assessmentId);
    }
}
