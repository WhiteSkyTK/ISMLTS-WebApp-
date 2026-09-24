using Microsoft.EntityFrameworkCore;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Data;

namespace ISMLTS_WebApp_.Repositories
{
    public class TicketRepository : Repository<Ticket>, ITicketRepository
    {
        public TicketRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Ticket>> GetByStudentAsync(int studentId) =>
            await _dbSet.Include(t => t.Module).Where(t => t.StudentId == studentId)
                .OrderByDescending(t => t.DateOpened).ToListAsync();

        public async Task<IEnumerable<Ticket>> GetByLecturerAsync(int lecturerId) =>
            await _dbSet.Include(t => t.Module).Include(t => t.Student)
                .Where(t => t.Module != null && t.Module.LecturerId == lecturerId)
                .OrderByDescending(t => t.DateOpened).ToListAsync();

        public async Task<Ticket?> GetByIdWithDetailsAsync(int id) =>
            await _dbSet.Include(t => t.Module).Include(t => t.Student).FirstOrDefaultAsync(t => t.TicketId == id);
    }
}