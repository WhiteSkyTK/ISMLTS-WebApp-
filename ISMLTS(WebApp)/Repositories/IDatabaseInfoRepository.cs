namespace ISMLTS_WebApp_.Repositories
{
    public record DatabaseInfo(bool Reachable, IReadOnlyList<string> PendingMigrations, string? LatestMigration, int Students, int Lecturers, int Modules, int Courses);

    public interface IDatabaseInfoRepository
    {
        Task<DatabaseInfo> GetAsync();
    }
}
