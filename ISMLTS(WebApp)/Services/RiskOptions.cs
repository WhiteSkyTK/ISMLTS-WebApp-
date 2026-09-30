namespace ISMLTS_WebApp_.Services
{
    // "Risk" config section
    public class RiskOptions
    {
        // A student is at risk below this attendance percentage (as well as below a 50% average)
        public int AttendanceThreshold { get; set; } = 75;
    }
}
