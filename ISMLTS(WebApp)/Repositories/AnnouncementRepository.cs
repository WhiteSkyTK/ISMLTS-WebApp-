using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public class AnnouncementRepository : Repository<Announcement>, IAnnouncementRepository
    {
        public AnnouncementRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Announcement?> GetByIdWithModuleAsync(int id) =>
            await _dbSet.Include(a => a.Module).FirstOrDefaultAsync(a => a.AnnouncementId == id);

        // Posts to the student's modules, plus college-wide posts for everyone or for students
        public async Task<List<Announcement>> GetForStudentAsync(IReadOnlyCollection<int> moduleIds, int take) =>
            await Newest()
                .Where(a => (a.ModuleId != null && moduleIds.Contains(a.ModuleId.Value))
                    || (a.ModuleId == null && (a.Audience == AnnouncementAudiences.Everyone || a.Audience == AnnouncementAudiences.Students)))
                .Take(take).ToListAsync();

        // Posts to the lecturer's own modules, plus college-wide posts for everyone or for lecturers
        public async Task<List<Announcement>> GetForLecturerAsync(IReadOnlyCollection<int> moduleIds, int take) =>
            await Newest()
                .Where(a => (a.ModuleId != null && moduleIds.Contains(a.ModuleId.Value))
                    || (a.ModuleId == null && (a.Audience == AnnouncementAudiences.Everyone || a.Audience == AnnouncementAudiences.Lecturers)))
                .Take(take).ToListAsync();

        public async Task<List<Announcement>> GetLatestAsync(int take) =>
            await Newest().Take(take).ToListAsync();

        public async Task<List<Announcement>> GetByAuthorAsync(string role, int authorId) =>
            await Newest().Where(a => a.AuthorRole == role && a.AuthorId == authorId).ToListAsync();

        private IQueryable<Announcement> Newest() =>
            _dbSet.AsNoTracking().Include(a => a.Module)
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.AnnouncementId);
    }
}
