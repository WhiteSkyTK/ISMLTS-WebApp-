using System.Globalization;
using System.Net;

namespace ISMLTS.Tests.Integration
{
    // Own factory: these tests add marks and submissions
    public class TeachingPagesTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public TeachingPagesTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);

        [Theory]
        [InlineData("/Marks/Delete/1")]
        [InlineData("/Assessments/Delete/1")]
        public async Task SeparateDeletePages_AreGone(string url)
        {
            var response = await _factory.ClientFor("Lecturer", _factory.Data.LecturerAId).GetAsync(url);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CapturingAMark_ShowsAToastOnTheModulePage()
        {
            var client = _factory.ClientFor("Lecturer", _factory.Data.LecturerAId);
            var moduleId = Id(_factory.Data.ModuleAId);
            var studentId = Id(_factory.Data.StudentId);
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, $"/Marks/Create?moduleId={moduleId}&studentId={studentId}");

            var response = await client.PostAsync("/Marks/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ModuleId"] = moduleId,
                ["StudentId"] = studentId,
                ["OtherName"] = "ICE Task 1",
                ["Score"] = "45.5",
                ["OutOf"] = "50",
                ["DateCaptured"] = "2026-09-01"
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var page = await client.GetStringAsync(response.Headers.Location);
            Assert.Contains("ICE Task 1 mark saved for Student A.", page);
            Assert.Contains("45.5/50", page);
        }

        [Fact]
        public async Task SubmittingWork_ShowsAToastAndTheModuleCode()
        {
            var client = _factory.ClientFor("Student", _factory.Data.StudentId);
            var assessmentId = Id(_factory.Data.AssessmentAId);
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, $"/Assessments/Submit/{assessmentId}");

            var response = await client.PostAsync($"/Assessments/Submit/{assessmentId}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["link"] = "https://github.com/student-a/poe"
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var page = await client.GetStringAsync(response.Headers.Location);
            Assert.Contains("Your work for POE A was submitted.", page);
            Assert.Contains(_factory.Data.ModuleACode, page);
        }
    }
}
