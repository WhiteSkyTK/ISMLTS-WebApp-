using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class NotificationHelpersTests
    {
        private static readonly DateTime Now = new(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);

        [Theory]
        [InlineData("/Marks/MyMarks", true)]
        [InlineData("/", true)]
        [InlineData("//evil.example/steal", false)]
        [InlineData("/\\evil.example", false)]
        [InlineData("https://evil.example", false)]
        [InlineData("javascript:alert(1)", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void LocalUrl_OnlyAllowsPathsOnThisSite(string? url, bool expected) =>
            Assert.Equal(expected, LocalUrl.IsLocal(url));

        [Theory]
        [InlineData(0, "Just now")]
        [InlineData(5, "5 min ago")]
        [InlineData(60, "1 hour ago")]
        [InlineData(180, "3 hours ago")]
        [InlineData(1500, "Yesterday")]
        [InlineData(4400, "3 days ago")]
        public void TimeAgo_DescribesRecentTimes(int minutesAgo, string expected) =>
            Assert.Equal(expected, TimeAgo.Describe(Now.AddMinutes(-minutesAgo), Now));

        [Fact]
        public void TimeAgo_OlderThanAWeek_ShowsTheDate() =>
            Assert.Matches(@"^\d{2} \w{3} \d{4}$", TimeAgo.Describe(Now.AddDays(-10), Now));

        [Theory]
        [InlineData(AnnouncementAudiences.Everyone, Roles.Student, true)]
        [InlineData(AnnouncementAudiences.Everyone, Roles.Lecturer, true)]
        [InlineData(AnnouncementAudiences.Students, Roles.Student, true)]
        [InlineData(AnnouncementAudiences.Students, Roles.Lecturer, false)]
        [InlineData(AnnouncementAudiences.Lecturers, Roles.Student, false)]
        [InlineData(AnnouncementAudiences.Lecturers, Roles.Admin, true)]
        public void AnnouncementAudience_CollegeWidePosts(string audience, string role, bool expected) =>
            Assert.Equal(expected, AnnouncementAudience.CanSee(new Announcement { Audience = audience }, role, Array.Empty<int>()));

        [Theory]
        [InlineData(new[] { 7 }, true)]
        [InlineData(new[] { 8 }, false)]
        [InlineData(new int[0], false)]
        public void AnnouncementAudience_ModulePostsNeedThatModule(int[] myModuleIds, bool expected) =>
            Assert.Equal(expected, AnnouncementAudience.CanSee(new Announcement { ModuleId = 7 }, Roles.Student, myModuleIds));
    }
}
