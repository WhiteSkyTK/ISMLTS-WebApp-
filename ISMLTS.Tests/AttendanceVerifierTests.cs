using System.Net;
using Microsoft.Extensions.Options;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class AttendanceVerifierTests
    {
        private const double ClassLat = -26.1929;
        private const double ClassLon = 28.0305;
        private static readonly IPAddress CampusIp = IPAddress.Parse("203.0.113.10");
        private static readonly IPAddress HomeIp = IPAddress.Parse("198.51.100.7");

        private static AttendanceVerifier CreateVerifier() => new(Options.Create(new AttendanceOptions
        {
            RadiusMeters = 200,
            AllowedIpRanges = new List<string> { "203.0.113.0/24", "not-a-range" }
        }));

        private static AttendanceSession SessionAt(double? latitude, double? longitude) =>
            new() { Latitude = latitude, Longitude = longitude };

        [Fact]
        public void CampusIp_PassesEvenWithoutLocation()
        {
            var check = CreateVerifier().Verify(SessionAt(ClassLat, ClassLon), CampusIp, null, null);

            Assert.True(check.IpOnCampus);
            Assert.True(check.Passed);
        }

        [Fact]
        public void IPv4MappedCampusIp_IsRecognised()
        {
            var check = CreateVerifier().Verify(SessionAt(null, null), IPAddress.Parse("::ffff:203.0.113.10"), null, null);

            Assert.True(check.IpOnCampus);
        }

        [Fact]
        public void OffCampus_WithinRadius_Passes()
        {
            // 0.001 degrees of latitude is about 111 m
            var check = CreateVerifier().Verify(SessionAt(ClassLat, ClassLon), HomeIp, ClassLat + 0.001, ClassLon);

            Assert.False(check.IpOnCampus);
            Assert.True(check.LocationVerified);
            Assert.InRange(check.DistanceMeters!.Value, 105d, 118d);
            Assert.True(check.Passed);
        }

        [Fact]
        public void OffCampus_FarAway_Fails()
        {
            // Pretoria, roughly 50 km from the classroom
            var check = CreateVerifier().Verify(SessionAt(ClassLat, ClassLon), HomeIp, -25.7479, 28.2293);

            Assert.False(check.LocationVerified);
            Assert.False(check.Passed);
        }

        [Fact]
        public void OffCampus_WithoutLocation_Fails()
        {
            var check = CreateVerifier().Verify(SessionAt(ClassLat, ClassLon), HomeIp, null, null);

            Assert.Null(check.DistanceMeters);
            Assert.False(check.Passed);
        }

        [Fact]
        public void LecturerWithoutLocation_SkipsDistanceCheck()
        {
            var check = CreateVerifier().Verify(SessionAt(null, null), HomeIp, ClassLat, ClassLon);

            Assert.Null(check.DistanceMeters);
            Assert.False(check.Passed);
        }

        [Fact]
        public void ImpossibleCoordinates_AreIgnored()
        {
            var check = CreateVerifier().Verify(SessionAt(ClassLat, ClassLon), HomeIp, 200, 500);

            Assert.Null(check.DistanceMeters);
            Assert.False(check.Passed);
        }
    }
}