using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface IAnnouncementRepository : IRepository<Announcement>
    {
        Task<Announcement?> GetByIdWithModuleAsync(int id);
        Task<List<Announcement>> GetForStudentAsync(IReadOnlyCollection<int> moduleIds, int take);
        Task<List<Announcement>> GetForLecturerAsync(IReadOnlyCollection<int> moduleIds, int take);
        Task<List<Announcement>> GetLatestAsync(int take);
        Task<List<Announcement>> GetByAuthorAsync(string role, int authorId);
    }
}
