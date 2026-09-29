using System.Net;

namespace ISMLTS.Tests.Integration
{
    public class AccessRulesTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public AccessRulesTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        public static TheoryData<string> AdminPages => new()
        {
            "/Students", "/Students/Create", "/Lecturers", "/Admins", "/Modules", "/Courses"
        };

        public static TheoryData<string> LecturerPages => new()
        {
            "/Marks", "/Assessments", "/Tickets", "/Attendance"
        };

        public static TheoryData<string> StudentPages => new()
        {
            "/Marks/MyMarks", "/Assessments/MyAssessments", "/Tickets/MyTickets", "/Attendance/MyAttendance"
        };

        [Theory]
        [MemberData(nameof(AdminPages))]
        [MemberData(nameof(LecturerPages))]
        [MemberData(nameof(StudentPages))]
        public async Task Anonymous_IsSentToLogin(string url)
        {
            var response = await _factory.ClientFor().GetAsync(url);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/Login", response.Headers.Location?.PathAndQuery);
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/Account/Login")]
        public async Task Anonymous_CanOpenPublicPages(string url)
        {
            var response = await _factory.ClientFor().GetAsync(url);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [MemberData(nameof(AdminPages))]
        [MemberData(nameof(LecturerPages))]
        public async Task Student_IsDeniedAdminAndLecturerPages(string url)
        {
            var client = _factory.ClientFor("Student", _factory.Data.StudentId);

            await AssertDeniedAsync(client, url);
        }

        [Theory]
        [MemberData(nameof(AdminPages))]
        [MemberData(nameof(StudentPages))]
        public async Task Lecturer_IsDeniedAdminAndStudentPages(string url)
        {
            var client = _factory.ClientFor("Lecturer", _factory.Data.LecturerAId);

            await AssertDeniedAsync(client, url);
        }

        [Theory]
        [MemberData(nameof(LecturerPages))]
        public async Task Admin_IsDeniedLecturerPages(string url)
        {
            var client = _factory.ClientFor("Admin", _factory.Data.AdminId);

            await AssertDeniedAsync(client, url);
        }

        [Theory]
        [MemberData(nameof(AdminPages))]
        public async Task Admin_CanOpenAdminPages(string url)
        {
            var response = await _factory.ClientFor("Admin", _factory.Data.AdminId).GetAsync(url);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Logout_ByGet_IsNotAllowed()
        {
            var response = await _factory.ClientFor("Student", _factory.Data.StudentId).GetAsync("/Account/Logout");

            // Conventional routing finds no GET action, so a link or <img> on another site can't sign anyone out
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Logout_ByPostWithoutToken_IsRejected()
        {
            var response = await _factory.ClientFor("Student", _factory.Data.StudentId)
                .PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        private static async Task AssertDeniedAsync(HttpClient client, string url)
        {
            var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/AccessDenied", response.Headers.Location?.PathAndQuery);

            var deniedPage = await client.GetAsync(response.Headers.Location);
            Assert.Equal(HttpStatusCode.Forbidden, deniedPage.StatusCode);
        }
    }
}
