using System.Globalization;
using System.Net;
using ISMLTS_WebApp_.Models;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    // Own factory: these tests add, release and delete marks
    public class MarkbookPageTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public MarkbookPageTests(IsmltsFactory factory)
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

        private Task<int> AddMarkAsync(decimal score) =>
            _factory.WithDbAsync(async db =>
            {
                var data = _factory.Data;
                var mark = new Mark { StudentId = data.StudentId, ModuleId = data.ModuleAId, AssessmentId = data.AssessmentAId, AssessmentName = "POE A", Score = score, MaxScore = 100 };
                db.Marks.Add(mark);
                await db.SaveChangesAsync();
                return mark.MarkId;
            });

        [Fact]
        public async Task Markbook_ShowsEachAssessmentWithAReleaseButton()
        {
            var data = _factory.Data;
            await AddMarkAsync(61.5m);

            var html = await _factory.ClientFor("Lecturer", data.LecturerAId).GetStringAsync($"/Marks/ForModule?moduleId={Id(data.ModuleAId)}");

            Assert.Contains("Markbook", html);
            Assert.Contains("POE A", html);
            Assert.Contains("Student A", html);
            Assert.Contains("61.5/100", html);
            Assert.Contains("Release marks", html);
            Assert.Contains($"/Grading/Gradebook/{Id(data.AssessmentAId)}", html);
        }

        [Fact]
        public async Task Release_GoesBackToALocalReturnUrl_ButNeverToAnotherSite()
        {
            var data = _factory.Data;
            var lecturer = _factory.ClientFor("Lecturer", data.LecturerAId);
            var markbook = $"/Marks/ForModule?moduleId={Id(data.ModuleAId)}";

            var released = await PostAsync(lecturer, markbook, $"/Assessments/Release/{Id(data.AssessmentAId)}",
                ("released", "true"), ("returnUrl", markbook));
            Assert.Equal(HttpStatusCode.Redirect, released.StatusCode);
            Assert.Equal(markbook, released.Headers.Location?.OriginalString);
            Assert.Contains("Hide marks", await lecturer.GetStringAsync(markbook));

            var hidden = await PostAsync(lecturer, markbook, $"/Assessments/Release/{Id(data.AssessmentAId)}",
                ("released", "false"), ("returnUrl", "https://evil.example/phish"));
            Assert.Equal(HttpStatusCode.Redirect, hidden.StatusCode);
            Assert.Equal($"/Assessments/ForModule?moduleId={Id(data.ModuleAId)}", hidden.Headers.Location?.OriginalString);
            Assert.False(await _factory.WithDbAsync(db => db.Assessments.Where(a => a.AssessmentId == data.AssessmentAId).Select(a => a.MarksReleased).SingleAsync()));
        }

        [Fact]
        public async Task Markbook_ForAnotherLecturersModule_IsNotFound()
        {
            var response = await _factory.ClientFor("Lecturer", _factory.Data.LecturerAId)
                .GetAsync($"/Marks/ForModule?moduleId={Id(_factory.Data.ModuleBId)}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task EditPage_DeletesTheMark()
        {
            var data = _factory.Data;
            var lecturer = _factory.ClientFor("Lecturer", data.LecturerAId);
            var markId = await AddMarkAsync(40);
            var editPage = $"/Marks/Edit/{Id(markId)}";

            Assert.Contains("Delete mark", await lecturer.GetStringAsync(editPage));
            var deleted = await PostAsync(lecturer, editPage, $"/Marks/Delete/{Id(markId)}");

            Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
            Assert.False(await _factory.WithDbAsync(db => db.Marks.AnyAsync(m => m.MarkId == markId)));
        }
    }
}
