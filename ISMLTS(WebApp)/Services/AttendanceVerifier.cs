using System.Net;
using Microsoft.Extensions.Options;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Services
{
    public record AttendanceCheck(bool IpOnCampus, double? DistanceMeters, bool LocationVerified)
    {
        public bool Passed => IpOnCampus || LocationVerified;
    }

    public interface IAttendanceVerifier
    {
        AttendanceCheck Verify(AttendanceSession session, IPAddress? ip, double? latitude, double? longitude);
    }

    public class AttendanceVerifier : IAttendanceVerifier
    {
        private const double EarthRadiusMeters = 6_371_000;
        private readonly double _radiusMeters;
        private readonly List<IPNetwork> _campusNetworks = new();

        public AttendanceVerifier(IOptions<AttendanceOptions> options)
        {
            _radiusMeters = options.Value.RadiusMeters;
            foreach (var range in options.Value.AllowedIpRanges)
            {
                if (IPNetwork.TryParse(range, out var network))
                {
                    _campusNetworks.Add(network);
                }
            }
        }

        public AttendanceCheck Verify(AttendanceSession session, IPAddress? ip, double? latitude, double? longitude)
        {
            var address = ip is { IsIPv4MappedToIPv6: true } ? ip.MapToIPv4() : ip;
            var onCampus = address != null && _campusNetworks.Exists(n => n.Contains(address));

            double? distance = null;
            if (session.Latitude is double classLat && session.Longitude is double classLon
                && latitude is double lat && longitude is double lon
                && IsValid(classLat, classLon) && IsValid(lat, lon))
            {
                distance = DistanceBetween(classLat, classLon, lat, lon);
            }

            return new AttendanceCheck(onCampus, distance, distance <= _radiusMeters);
        }

        private static bool IsValid(double latitude, double longitude) =>
            latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

        // Haversine formula: straight-line distance between two GPS points, in metres
        private static double DistanceBetween(double lat1, double lon1, double lat2, double lon2)
        {
            static double ToRadians(double degrees) => degrees * Math.PI / 180;

            var sinLat = Math.Sin(ToRadians(lat2 - lat1) / 2);
            var sinLon = Math.Sin(ToRadians(lon2 - lon1) / 2);
            var a = sinLat * sinLat + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * sinLon * sinLon;
            return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}
