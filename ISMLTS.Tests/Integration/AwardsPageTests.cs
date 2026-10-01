using System.Net;

namespace ISMLTS.Tests.Integration
{
    public class AwardsPageTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public AwardsPageTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Students_SeeEveryBadgeAndHowToEarnIt()
        {
            var html = await _factory.ClientFor("Student", _factory.Data.StudentId).GetStringAsync("/Awards");

            Assert.Contains("Always there", html);
            Assert.Contains("Top of the class", html);
            Assert.Contains("Never late", html);
            Assert.Contains("Distinction", html);
        }

        [Fact]
        public async Task Lecturers_CannotOpenAwards()
        {
            var response = await _factory.ClientFor("Lecturer", _factory.Data.LecturerAId).GetAsync("/Awards");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/AccessDenied", response.Headers.Location?.PathAndQuery);
        }
    }
}
