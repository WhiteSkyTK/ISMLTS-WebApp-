using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public class AttendanceRepository : Repository<AttendanceSession>, IAttendanceRepository
    {
        private readonly DbSet<AttendanceRecord> _records;

        public AttendanceRepository(ApplicationDbContext context) : base(context)
        {
            _records = context.Set<AttendanceRecord>();
        }

        public async Task<AttendanceSession?> GetByCodeAsync(string code) =>
            await _dbSet.Include(s => s.Module).FirstOrDefaultAsync(s => s.Code == code);

        public async Task<AttendanceSession?> GetWithRecordsAsync(int sessionId) =>
            await _dbSet.Include(s => s.Module)
                .Include(s => s.Records).ThenInclude(r => r.Student)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        public async Task<IEnumerable<AttendanceSession>> GetByModuleAsync(int moduleId) =>
            await _dbSet.Include(s => s.Records)
                .Where(s => s.ModuleId == moduleId)
                .OrderByDescending(s => s.StartedAt)
                .ToListAsync();

        public async Task<int> CountByModuleAsync(int moduleId) =>
            await _dbSet.CountAsync(s => s.ModuleId == moduleId);

        public async Task<bool> CodeExistsAsync(string code) =>
            await _dbSet.AnyAsync(s => s.Code == code);

        public async Task<bool> HasScannedAsync(int sessionId, int studentId) =>
            await _records.AnyAsync(r => r.SessionId == sessionId && r.StudentId == studentId);

        public async Task AddRecordAsync(AttendanceRecord record) => await _records.AddAsync(record);

        public void RemoveRecord(AttendanceRecord record) => _records.Remove(record);

        public async Task<IEnumerable<AttendanceRecord>> GetRecordsByStudentAsync(int studentId, AttendancePeriods periods)
        {
            var counting = Counting(periods).Select(s => s.SessionId);
            return await _records.Include(r => r.Session)
                .Where(r => r.StudentId == studentId && counting.Contains(r.SessionId))
                .ToListAsync();
        }

        public async Task<List<AttendanceRecord>> GetRecordsByModulesAsync(IReadOnlyCollection<int> moduleIds, AttendancePeriods periods)
        {
            var counting = Counting(periods).Where(s => moduleIds.Contains(s.ModuleId)).Select(s => s.SessionId);
            return await _records.AsNoTracking().Include(r => r.Session)
                .Where(r => counting.Contains(r.SessionId))
                .ToListAsync();
        }

        public async Task<Dictionary<int, int>> CountSessionsByModuleAsync(IReadOnlyCollection<int> moduleIds, AttendancePeriods periods) =>
            await Counting(periods).Where(s => moduleIds.Contains(s.ModuleId))
                .GroupBy(s => s.ModuleId)
                .Select(g => new { ModuleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ModuleId, x => x.Count);

        // Sessions that count towards attendance: not cancelled, and inside the term window for their module's term code
        private IQueryable<AttendanceSession> Counting(AttendancePeriods periods)
        {
            var sessions = _dbSet.Where(s => !s.IsCancelled);
            foreach (var (code, window) in periods.Windows)
            {
                var from = window.FromUtc;
                var to = window.ToUtc;
                sessions = sessions.Where(s => s.Module!.Term != code || (s.StartedAt >= from && s.StartedAt < to));
            }
            return sessions;
        }
    }
}