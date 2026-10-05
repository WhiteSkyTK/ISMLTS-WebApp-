using Microsoft.Extensions.Options;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    // Privacy:ScanDetailsDays: how long the IP address and location of an attendance scan are kept (the privacy
    // notice shows this number). After that only the result stays: present, on campus or not, location confirmed or not.
    public class PrivacyOptions
    {
        public int ScanDetailsDays { get; set; } = 90;

        public int Days => Math.Clamp(ScanDetailsDays, 1, 3650);
    }

    public interface IScanDetailsCleaner
    {
        Task<int> ClearAsync(DateTime nowUtc);
    }

    public class ScanDetailsCleaner : IScanDetailsCleaner
    {
        private readonly IAttendanceRepository _attendance;
        private readonly PrivacyOptions _options;

        public ScanDetailsCleaner(IAttendanceRepository attendance, IOptions<PrivacyOptions> options)
        {
            _attendance = attendance;
            _options = options.Value;
        }

        public Task<int> ClearAsync(DateTime nowUtc) => _attendance.ClearScanDetailsAsync(nowUtc.AddDays(-_options.Days));
    }

    // Runs a minute after start-up and then every six hours
    public class ScanDetailsCleanupService : BackgroundService
    {
        private static readonly TimeSpan CheckEvery = TimeSpan.FromHours(6);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<ScanDetailsCleanupService> _logger;

        public ScanDetailsCleanupService(IServiceScopeFactory scopes, ILogger<ScanDetailsCleanupService> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            using var timer = new PeriodicTimer(CheckEvery);
            do
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var cleared = await scope.ServiceProvider.GetRequiredService<IScanDetailsCleaner>().ClearAsync(DateTime.UtcNow);
                    if (cleared > 0) _logger.LogInformation("Cleared the IP and location details of {Count} old attendance scans", cleared);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogWarning(e, "Clearing old attendance scan details failed; trying again later");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
