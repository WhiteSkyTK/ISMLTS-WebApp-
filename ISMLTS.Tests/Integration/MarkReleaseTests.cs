using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    // Own factory: these tests add marks and release them
    public class MarkReleaseTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public MarkReleaseTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string formPage, string url, params (string Key, string Value)[] fields)
        {
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, formPage);
            return await client.PostAsync(url, new FormUrlEncodedContent(
                fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value))
                    .Prepend(new KeyValuePair<string, string>("__RequestVerificationToken", token))));
        }

        [Fact]
        public async Task StudentsSeeAnAssessmentMark_OnlyAfterItIsReleased()
        {
            var data = _factory.Data;
            var lecturer = _factory.ClientFor("Lecturer", data.LecturerAId);
            var student = _factory.ClientFor("Student", data.StudentId);

            var created = await PostAsync(lecturer, $"/Marks/Create?moduleId={Id(data.ModuleAId)}&studentId={Id(data.StudentId)}", "/Marks/Create",
                ("ModuleId", Id(data.ModuleAId)), ("StudentId", Id(data.StudentId)), ("AssessmentId", Id(data.AssessmentAId)),
                ("Score", "73"), ("Feedback", "Strong analysis."), ("DateCaptured", "2026-09-30"));
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

            Assert.DoesNotContain("Strong analysis.", await student.GetStringAsync("/Marks/MyMarks"));
            Assert.DoesNotContain("Strong analysis.", await student.GetStringAsync("/Assessments/MyAssessments"));
            Assert.False(await _factory.WithDbAsync(db => db.Notifications.AnyAsync(n => n.UserId == data.StudentId)));

            var released = await PostAsync(lecturer, $"/Assessments/ForModule?moduleId={Id(data.ModuleAId)}", $"/Assessments/Release/{Id(data.AssessmentAId)}",
                ("released", "true"));
            Assert.Equal(HttpStatusCode.Redirect, released.StatusCode);

            Assert.Contains("Strong analysis.", await student.GetStringAsync("/Marks/MyMarks"));
            var myAssessments = await student.GetStringAsync("/Assessments/MyAssessments");
            Assert.Contains("73/100", myAssessments);
            Assert.True(await _factory.WithDbAsync(db => db.Notifications.AnyAsync(n => n.UserId == data.StudentId && n.Title == "Marks released: POE A")));
        }

        [Fact]
        public async Task AMarkForAnotherModulesAssessment_IsRejected()
        {
            var data = _factory.Data;
            var lecturer = _factory.ClientFor("Lecturer", data.LecturerAId);

            var response = await PostAsync(lecturer, $"/Marks/Create?moduleId={Id(data.ModuleAId)}&studentId={Id(data.StudentId)}", "/Marks/Create",
                ("ModuleId", Id(data.ModuleAId)), ("StudentId", Id(data.StudentId)), ("AssessmentId", Id(data.AssessmentBId)),
                ("Score", "10"), ("DateCaptured", "2026-09-30"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Pick an assessment from this module.", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task AnotherLecturer_CannotReleaseMarks()
        {
            var data = _factory.Data;
            var intruder = _factory.ClientFor("Lecturer", data.LecturerBId);

            var response = await PostAsync(intruder, $"/Assessments/ForModule?moduleId={Id(data.ModuleBId)}", $"/Assessments/Release/{Id(data.AssessmentAId)}",
                ("released", "true"));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
