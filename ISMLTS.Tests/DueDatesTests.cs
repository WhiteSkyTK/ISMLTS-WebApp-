using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class DueDatesTests
    {
        private static readonly DateTime Today = new(2026, 10, 1, 14, 30, 0);

        [Theory]
        [InlineData(0, "Due today")]
        [InlineData(1, "Due tomorrow")]
        [InlineData(5, "Due in 5 days")]
        [InlineData(-1, "1 day overdue")]
        [InlineData(-3, "3 days overdue")]
        public void Describe_UsesWholeCalendarDays(int offset, string expected) =>
            Assert.Equal(expected, DueDates.Describe(Today.Date.AddDays(offset), Today));

        [Fact]
        public void IsOverdue_IsFalseOnTheDueDateItself()
        {
            Assert.False(DueDates.IsOverdue(Today.Date, Today));
            Assert.True(DueDates.IsOverdue(Today.Date.AddDays(-1), Today));
        }
    }
}
