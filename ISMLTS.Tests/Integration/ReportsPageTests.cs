using System.Net;

namespace ISMLTS.Tests.Integration
{
    public class ReportsPageTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public ReportsPageTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Admins_SeeUsersAndEveryModule()
        {
            var html = await _factory.ClientFor("Admin", _factory.Data.AdminId).GetStringAsync("/Reports");

            Assert.Contains("Use two-factor sign-in", html);
            Assert.Contains(_factory.Data.ModuleACode, html);
            Assert.Contains(_factory.Data.ModuleBCode, html);
        }

        [Fact]
        public async Task Export_IsACsvOfModules()
        {
            var response = await _factory.ClientFor("Admin", _factory.Data.AdminId).GetAsync("/Reports/Export");

            Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
            var csv = await response.Content.ReadAsStringAsync();
            Assert.Contains("Module,Name,Course,Term,Lecturer", csv);
            Assert.Contains(_factory.Data.ModuleACode, csv);
        }

        [Fact]
        public async Task Lecturers_CannotSeeReports()
        {
            var response = await _factory.ClientFor("Lecturer", _factory.Data.LecturerAId).GetAsync("/Reports");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/AccessDenied", response.Headers.Location?.PathAndQuery);
        }
    }
}
