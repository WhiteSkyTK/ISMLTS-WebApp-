using System.Security.Claims;
using ISMLTS_WebApp_.Extensions;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    public interface IAuditLog
    {
        Task RecordAsync(ClaimsPrincipal actor, string action, string target, string? details = null);
    }

    public class AuditLog : IAuditLog
    {
        private readonly IAuditRepository _entries;
        private readonly TimeProvider _time;

        public AuditLog(IAuditRepository entries, TimeProvider time)
        {
            _entries = entries;
            _time = time;
        }

        public async Task RecordAsync(ClaimsPrincipal actor, string action, string target, string? details = null)
        {
            await _entries.AddAsync(new AuditEntry
            {
                At = _time.GetUtcNow().UtcDateTime,
                ActorRole = Role(actor),
                ActorId = actor.GetUserId() ?? 0,
                ActorName = Cut(actor.Identity?.Name ?? "unknown", 100),
                Action = action,
                Target = Cut(target, 200),
                Details = details == null ? null : Cut(details, 500)
            });
            await _entries.SaveChangesAsync();
        }

        private static string Role(ClaimsPrincipal actor) =>
            actor.IsInRole(Roles.Admin) ? Roles.Admin : actor.IsInRole(Roles.Lecturer) ? Roles.Lecturer : actor.IsInRole(Roles.Student) ? Roles.Student : "System";

        private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
    }
}
