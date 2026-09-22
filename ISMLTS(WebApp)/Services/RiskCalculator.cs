using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Services
{
    public static class RiskCalculator
    {
        public const decimal AtRiskThreshold = 50m;

        public static decimal AveragePercentage(IEnumerable<Mark> marks)
        {
            var list = marks.ToList();
            return list.Any() ? Math.Round(list.Average(m => m.Percentage), 1) : 0;
        }

        public static bool IsAtRisk(IEnumerable<Mark> marks) => AveragePercentage(marks) < AtRiskThreshold;
    }
}