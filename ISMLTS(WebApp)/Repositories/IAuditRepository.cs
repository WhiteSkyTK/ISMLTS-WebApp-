using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Repositories
{
    public interface IAuditRepository : IRepository<AuditEntry>
    {
        // Newest first; q matches the actor, target or details, action narrows to one kind
        Task<PagedList<AuditEntry>> SearchAsync(string? query, int page, string? action);
    }
}
