using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Data;

namespace ISMLTS_WebApp_.Repositories
{
    public class MarkRepository : Repository<Mark>, IMarkRepository
    {
        public MarkRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Mark>> GetByModuleAsync(int moduleId) =>
            await _dbSet.Include(m => m.Assessment).Include(m => m.Student)
                .Where(m => m.ModuleId == moduleId).ToListAsync();

        public async Task<IEnumerable<Mark>> GetByModulesAsync(IReadOnlyCollection<int> moduleIds) =>
            await _dbSet.AsNoTracking().Include(m => m.Assessment)
                .Where(m => moduleIds.Contains(m.ModuleId)).ToListAsync();

        public async Task<IEnumerable<Mark>> GetByStudentAsync(int studentId) =>
            await _dbSet.Include(m => m.Module).Include(m => m.Assessment)
                .Where(m => m.StudentId == studentId).ToListAsync();

        public async Task<Mark?> GetByIdWithDetailsAsync(int id) =>
            await _dbSet.Include(m => m.Student).Include(m => m.Module).Include(m => m.Assessment)
                .FirstOrDefaultAsync(m => m.MarkId == id);

        // Tracked, so the gradebook and imports can update them in place
        public async Task<List<Mark>> GetByAssessmentAsync(int assessmentId) =>
            await _dbSet.Where(m => m.AssessmentId == assessmentId).ToListAsync();

        public async Task<Mark?> GetByAssessmentAndStudentAsync(int assessmentId, int studentId) =>
            await _dbSet.FirstOrDefaultAsync(m => m.AssessmentId == assessmentId && m.StudentId == studentId);

        public async Task<HashSet<(int AssessmentId, int StudentId)>> GetMarkedPairsAsync(IReadOnlyCollection<int> assessmentIds)
        {
            var pairs = await _dbSet.Where(m => m.AssessmentId != null && assessmentIds.Contains(m.AssessmentId.Value))
                .Select(m => new { AssessmentId = m.AssessmentId!.Value, m.StudentId })
                .ToListAsync();
            return pairs.Select(p => (p.AssessmentId, p.StudentId)).ToHashSet();
        }

        // Keeps the marks (and their names) when the assessment they belonged to is deleted
        public async Task UnlinkAssessmentAsync(int assessmentId) =>
            await _dbSet.Where(m => m.AssessmentId == assessmentId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.AssessmentId, (int?)null));
    }
}
