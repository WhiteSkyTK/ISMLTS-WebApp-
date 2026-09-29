using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    public class OwnershipTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public OwnershipTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private HttpClient LecturerA => _factory.ClientFor("Lecturer", _factory.Data.LecturerAId);

        public static TheoryData<string> LecturerBPages => new()
        {
            "/Marks/ForModule?moduleId={moduleB}",
            "/Marks/Edit/{markB}",
            "/Assessments/ForModule?moduleId={moduleB}",
            "/Assessments/Create?moduleId={moduleB}",
            "/Assessments/Edit/{assessmentB}",
            "/Assessments/Submissions/{assessmentB}",
            "/Attendance/ForModule?moduleId={moduleB}",
            "/Tickets/Respond/{ticketB}"
        };

        [Theory]
        [MemberData(nameof(LecturerBPages))]
        public async Task Lecturer_CannotOpenAnotherLecturersModule(string template)
        {
            var response = await LecturerA.GetAsync(Fill(template));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [InlineData("/Marks/ForModule?moduleId={moduleA}")]
        [InlineData("/Assessments/ForModule?moduleId={moduleA}")]
        [InlineData("/Assessments/Submissions/{assessmentA}")]
        [InlineData("/Attendance/ForModule?moduleId={moduleA}")]
        public async Task Lecturer_CanOpenOwnModule(string template)
        {
            var response = await LecturerA.GetAsync(Fill(template));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Lecturer_ModulePickerListsOnlyOwnModules()
        {
            var html = await LecturerA.GetStringAsync("/Marks");

            Assert.Contains(_factory.Data.ModuleACode, html);
            Assert.DoesNotContain(_factory.Data.ModuleBCode, html);
        }

        [Fact]
        public async Task Lecturer_CannotDeleteAnotherLecturersMark()
        {
            var client = LecturerA;
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, Fill("/Marks/ForModule?moduleId={moduleA}"));
            var markId = _factory.Data.MarkBId;

            var response = await client.PostAsync($"/Marks/Delete/{markId}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));

            Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode); // the token was accepted
            Assert.True(await _factory.WithDbAsync(db => db.Marks.AnyAsync(m => m.MarkId == markId)));
        }

        [Fact]
        public async Task Student_CannotSubmitToAModuleTheyAreNotEnrolledIn()
        {
            var response = await _factory.ClientFor("Student", _factory.Data.StudentId)
                .GetAsync($"/Assessments/Submit/{_factory.Data.AssessmentBId}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Student_CanOpenSubmitForOwnModule()
        {
            var response = await _factory.ClientFor("Student", _factory.Data.StudentId)
                .GetAsync($"/Assessments/Submit/{_factory.Data.AssessmentAId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private string Fill(string template)
        {
            var d = _factory.Data;
            return template
                .Replace("{moduleA}", d.ModuleAId.ToString(CultureInfo.InvariantCulture))
                .Replace("{moduleB}", d.ModuleBId.ToString(CultureInfo.InvariantCulture))
                .Replace("{assessmentA}", d.AssessmentAId.ToString(CultureInfo.InvariantCulture))
                .Replace("{assessmentB}", d.AssessmentBId.ToString(CultureInfo.InvariantCulture))
                .Replace("{markB}", d.MarkBId.ToString(CultureInfo.InvariantCulture))
                .Replace("{ticketB}", d.TicketBId.ToString(CultureInfo.InvariantCulture));
        }
    }
}
