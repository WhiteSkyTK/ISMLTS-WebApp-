using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class RiskCalculatorTests
    {
        private static Mark MarkOf(decimal score, decimal maxScore = 100) =>
            new() { AssessmentName = "Test", Score = score, MaxScore = maxScore };

        [Fact]
        public void AveragePercentage_WithNoMarks_IsZero() =>
            Assert.Equal(0m, RiskCalculator.AveragePercentage(Array.Empty<Mark>()));

        [Fact]
        public void AveragePercentage_UsesPercentagesNotRawScores()
        {
            var marks = new[] { MarkOf(40, 50), MarkOf(30, 100) }; // 80% and 30%

            Assert.Equal(55m, RiskCalculator.AveragePercentage(marks));
        }

        [Theory]
        [InlineData(49.9, true)]
        [InlineData(50.0, false)]
        [InlineData(82.0, false)]
        public void IsAtRisk_FlagsAveragesBelowFifty(double score, bool expected) =>
            Assert.Equal(expected, RiskCalculator.IsAtRisk(new[] { MarkOf((decimal)score) }));
    }
}