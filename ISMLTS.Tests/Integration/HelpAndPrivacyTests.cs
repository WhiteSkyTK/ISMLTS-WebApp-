using System.Net;

namespace ISMLTS.Tests.Integration
{
    public class HelpAndPrivacyTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public HelpAndPrivacyTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task SignedOutVisitors_GetTheLoginQuestionsOnly()
        {
            var response = await _factory.ClientFor().GetAsync("/Help");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("I forgot my password", html);
            Assert.DoesNotContain("For students", html);
            Assert.DoesNotContain("For admins", html);
        }

        [Theory]
        [InlineData("Student", "For students", "Raise a ticket")]
        [InlineData("Lecturer", "For lecturers", "Contact the admin office")]
        [InlineData("Admin", "For admins", "Contact the admin office")]
        public async Task EachRole_GetsItsOwnQuestions(string role, string section, string stillStuck)
        {
            var data = _factory.Data;
            var id = role switch { "Admin" => data.AdminId, "Lecturer" => data.LecturerAId, _ => data.StudentId };

            var html = await _factory.ClientFor(role, id).GetStringAsync("/Help");

            Assert.Contains(section, html);
            Assert.Contains(stillStuck, html);
        }

        [Fact]
        public async Task PrivacyNotice_IsPublic_AndCoversAttendanceScans()
        {
            var response = await _factory.ClientFor().GetAsync("/Home/Privacy");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("IP address", html);
            Assert.Contains("location", html);
            Assert.Contains("Information Regulator", html);
        }
    }
}
