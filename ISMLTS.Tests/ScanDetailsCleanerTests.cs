using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ISMLTS.Tests
{
    public class ScanDetailsCleanerTests : IDisposable
    {
        private readonly SqliteTestDb _db = new();

        [Fact]
        public async Task OldScans_LoseTheirIpAndLocation_ButKeepTheResult()
        {
            var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
            await using (var setup = _db.NewContext())
            {
                var module = new Module { Code = "PROG6211", Name = "Programming", Lecturer = new Lecturer { FullName = "L", Email = "l@x.test", PasswordHash = "x" } };
                var student = new Student { FullName = "S", Email = "s@rcconnect.edu.za", PasswordHash = "x" };
                AttendanceRecord Scan(int daysAgo) => new()
                {
                    Student = student, ScannedAt = now.AddDays(-daysAgo), IpAddress = "41.13.20.5", IpOnCampus = true,
                    Latitude = -26.1, Longitude = 28.0, AccuracyMeters = 12, DistanceMeters = 40, LocationVerified = true
                };
                setup.Add(new AttendanceSession { Module = module, Code = "OLD001", StartedAt = now.AddDays(-100), ExpiresAt = now.AddDays(-100), Records = { Scan(100) } });
                setup.Add(new AttendanceSession { Module = module, Code = "NEW001", StartedAt = now.AddDays(-10), ExpiresAt = now.AddDays(-10), Records = { Scan(10) } });
                await setup.SaveChangesAsync();
            }

            await using var context = _db.NewContext();
            var cleaner = new ScanDetailsCleaner(new AttendanceRepository(context), Options.Create(new PrivacyOptions { ScanDetailsDays = 90 }));

            Assert.Equal(1, await cleaner.ClearAsync(now));
            Assert.Equal(0, await cleaner.ClearAsync(now));

            await using var check = _db.NewContext();
            var records = await check.AttendanceRecords.OrderBy(r => r.ScannedAt).ToListAsync();
            Assert.Equal((null, null, null, null, null), (records[0].IpAddress, records[0].Latitude, records[0].Longitude, records[0].AccuracyMeters, records[0].DistanceMeters));
            Assert.True(records[0].IpOnCampus && records[0].LocationVerified);
            Assert.Equal("41.13.20.5", records[1].IpAddress);
        }

        [Fact]
        public void TheRetentionPeriod_StaysSensible()
        {
            Assert.Equal(90, new PrivacyOptions().Days);
            Assert.Equal(1, new PrivacyOptions { ScanDetailsDays = 0 }.Days);
        }

        public void Dispose() => _db.Dispose();
    }
}
