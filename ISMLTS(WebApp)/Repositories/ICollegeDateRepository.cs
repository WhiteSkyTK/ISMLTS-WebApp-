using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface ICollegeDateRepository : IRepository<CollegeDate>
    {
        Task<List<CollegeDate>> GetOrderedAsync();
        Task<List<CollegeDate>> GetOverlappingAsync(DateTime from, DateTime to);
    }
}
