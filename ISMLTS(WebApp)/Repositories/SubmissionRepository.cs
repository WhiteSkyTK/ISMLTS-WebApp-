using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Data;

namespace ISMLTS_WebApp_.Repositories
{
    public class SubmissionRepository : Repository<Submission>, ISubmissionRepository
    {
        public SubmissionRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Submission>> GetByAssessmentAsync(int assessmentId) =>
            await _dbSet.Include(s => s.Assessment).Where(s => s.AssessmentId == assessmentId).ToListAsync();

        public async Task<Submission?> GetByAssessmentAndStudentAsync(int assessmentId, int studentId) =>
            await _dbSet.Include(s => s.Assessment).FirstOrDefaultAsync(s => s.AssessmentId == assessmentId && s.StudentId == studentId);

        public async Task<IEnumerable<Submission>> GetByStudentAsync(int studentId) =>
            await _dbSet.Include(s => s.Assessment).Where(s => s.StudentId == studentId).ToListAsync();
    }
}