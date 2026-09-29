using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface IMarkChangeRepository : IRepository<MarkChange>
    {
        Task<List<MarkChange>> GetByMarkAsync(int markId);
    }
}
