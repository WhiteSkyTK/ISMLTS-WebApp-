using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public class SubmissionFileRepository : Repository<SubmissionFile>, ISubmissionFileRepository
    {
        public SubmissionFileRepository(ApplicationDbContext context) : base(context) { }

        public async Task<SubmissionFile?> GetWithOwnersAsync(int fileId) =>
            await _dbSet.AsNoTracking()
                .Include(f => f.Submission).ThenInclude(s => s!.Assessment).ThenInclude(a => a!.Module)
                .FirstOrDefaultAsync(f => f.SubmissionFileId == fileId);

        public async Task<List<string>> GetStoredNamesForStudentAsync(int studentId) =>
            await _dbSet.Where(f => f.Submission!.StudentId == studentId).Select(f => f.StoredName).ToListAsync();

        public async Task<List<string>> GetStoredNamesForModuleAsync(int moduleId) =>
            await _dbSet.Where(f => f.Submission!.Assessment!.ModuleId == moduleId).Select(f => f.StoredName).ToListAsync();

        public async Task<List<string>> GetStoredNamesForAssessmentAsync(int assessmentId) =>
            await _dbSet.Where(f => f.Submission!.AssessmentId == assessmentId).Select(f => f.StoredName).ToListAsync();
    }
}
