using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface ITicketRepository : IRepository<Ticket>
    {
        Task<IEnumerable<Ticket>> GetByStudentAsync(int studentId);
        Task<IEnumerable<Ticket>> GetByLecturerAsync(int lecturerId);
        Task<Ticket?> GetByIdWithDetailsAsync(int id);
    }
}