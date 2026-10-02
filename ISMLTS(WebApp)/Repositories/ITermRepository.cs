using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface ITermRepository : IRepository<Term>
    {
        // Newest first
        Task<List<Term>> GetOrderedAsync();
        Task<bool> NameExistsAsync(string name, int exceptTermId = 0);
    }
}
