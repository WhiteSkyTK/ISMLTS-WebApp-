using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface IAttendanceRepository : IRepository<AttendanceSession>
    {
        Task<AttendanceSession?> GetByCodeAsync(string code);
        Task<AttendanceSession?> GetWithRecordsAsync(int sessionId);
        Task<IEnumerable<AttendanceSession>> GetByModuleAsync(int moduleId);
        Task<int> CountByModuleAsync(int moduleId);
        Task<bool> CodeExistsAsync(string code);
        Task<bool> HasScannedAsync(int sessionId, int studentId);
        Task AddRecordAsync(AttendanceRecord record);
        void RemoveRecord(AttendanceRecord record);
        Task<IEnumerable<AttendanceRecord>> GetRecordsByStudentAsync(int studentId);
    }
}