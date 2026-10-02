namespace ISMLTS_WebApp_.Models
{
    // The stretch of time attendance is counted over for each module term code (Term1/Term2), in UTC.
    // A code without a window counts every session, so a site with no terms set up behaves as before.
    public record AttendancePeriods(IReadOnlyDictionary<string, (DateTime FromUtc, DateTime ToUtc)> Windows)
    {
        public static readonly AttendancePeriods All = new(new Dictionary<string, (DateTime, DateTime)>());
    }
}
