using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface ITimetableRepository : IRepository<TimetableSlot>
    {
        // With their modules, in weekday and time order
        Task<List<TimetableSlot>> GetByModulesAsync(IReadOnlyCollection<int> moduleIds);
        Task<TimetableSlot?> GetWithModuleAsync(int slotId);
    }
}
