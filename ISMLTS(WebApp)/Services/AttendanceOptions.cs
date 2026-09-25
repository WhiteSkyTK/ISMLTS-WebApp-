namespace ISMLTS_WebApp_.Services
{
    public class AttendanceOptions
    {
        public int SessionMinutes { get; set; } = 15;
        public double RadiusMeters { get; set; } = 200;
        public List<string> AllowedIpRanges { get; set; } = new();
    }
}