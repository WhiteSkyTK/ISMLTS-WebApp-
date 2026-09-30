using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    // Own factory: these tests save marks
    public class GradingPagesTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public GradingPagesTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);

        private async Task<HttpResponseMessage> PostAsync(HttpClient client, string formPage, string url, params (string Key, string Value)[] fields)
        {
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, formPage);
            return await client.PostAsync(url, new FormUrlEncodedContent(
                fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value))
                    .Prepend(new KeyValuePair<string, string>("__RequestVerificationToken", token))));
        }

        [Fact]
        public async Task Gradebook_WithOneBadRow_SavesNothing_ThenSavesWhenFixed()
        {
            var data = _factory.Data;
            var lecturer = _factory.ClientFor("Lecturer", data.LecturerAId);
            var page = $"/Grading/Gradebook/{Id(data.AssessmentAId)}";

            var bad = await PostAsync(lecturer, page, page,
                ("Rows[0].StudentId", Id(data.StudentId)), ("Rows[0].Score", "150"), ("Rows[0].Feedback", "Too high"));
            Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
            var html = await bad.Content.ReadAsStringAsync();
            Assert.Contains("Nothing was saved.", html);
            Assert.Contains("The score can&#x27;t be more than 100.", html);
            Assert.False(await _factory.WithDbAsync(db => db.Marks.AnyAsync(m => m.AssessmentId == data.AssessmentAId)));

            var good = await PostAsync(lecturer, page, page,
                ("Rows[0].StudentId", Id(data.StudentId)), ("Rows[0].Score", "64.5"), ("Rows[0].Feedback", "Good work"));
            Assert.Equal(HttpStatusCode.Redirect, good.StatusCode);
            var mark = await _factory.WithDbAsync(db => db.Marks.SingleAsync(m => m.AssessmentId == data.AssessmentAId && m.StudentId == data.StudentId));
            Assert.Equal((64.5m, 100m, "Good work"), (mark.Score, mark.MaxScore, mark.Feedback));
            Assert.Contains("Saved 1 mark(s) for POE A.", await lecturer.GetStringAsync(good.Headers.Location));
        }

        [Fact]
        public async Task QuickEval_ShowsUnmarkedWork_AndSaveAndNextEmptiesTheQueue()
        {
            var data = _factory.Data;
            await _factory.WithDbAsync(async db =>
            {
                db.Submissions.Add(new ISMLTS_WebApp_.Models.Submission { AssessmentId = data.AssessmentAId, StudentId = data.StudentId, Link = "https://github.com/a/poe", SubmittedAt = DateTime.UtcNow });
                return await db.SaveChangesAsync();
            });
            // Clear any mark another test in this class left for the same assessment
            await _factory.WithDbAsync(db => db.Marks.Where(m => m.AssessmentId == data.AssessmentAId).ExecuteDeleteAsync());
            var lecturer = _factory.ClientFor("Lecturer", data.LecturerAId);

            var queue = await lecturer.GetStringAsync("/Grading/QuickEval");
            Assert.Contains("Student A", queue);
            Assert.Contains("https://github.com/a/poe", queue);

            var tooHigh = await PostAsync(lecturer, "/Grading/QuickEval", "/Grading/QuickEval",
                ("assessmentId", Id(data.AssessmentAId)), ("studentId", Id(data.StudentId)), ("score", "101"), ("skip", "0"));
            Assert.Equal(HttpStatusCode.OK, tooHigh.StatusCode);
            Assert.Contains("The score can&#x27;t be more than 100.", await tooHigh.Content.ReadAsStringAsync());

            var saved = await PostAsync(lecturer, "/Grading/QuickEval", "/Grading/QuickEval",
                ("assessmentId", Id(data.AssessmentAId)), ("studentId", Id(data.StudentId)), ("score", "77"), ("feedback", "Clear and complete."), ("skip", "0"));
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            Assert.Contains("All caught up", await lecturer.GetStringAsync(saved.Headers.Location));

            var change = await _factory.WithDbAsync(db => db.MarkChanges.OrderByDescending(c => c.MarkChangeId).FirstAsync(c => c.StudentId == data.StudentId));
            Assert.Equal(("Quick Eval", (decimal?)77m), (change.Source, change.NewScore));
        }

        [Fact]
        public async Task Gradebook_RejectsStudentsFromOutsideTheModule()
        {
            var data = _factory.Data;
            var lecturer = _factory.ClientFor("Lecturer", data.LecturerAId);
            var page = $"/Grading/Gradebook/{Id(data.AssessmentAId)}";

            var response = await PostAsync(lecturer, page, page, ("Rows[0].StudentId", Id(data.OtherStudentId)), ("Rows[0].Score", "50"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(await _factory.WithDbAsync(db => db.Marks.AnyAsync(m => m.StudentId == data.OtherStudentId && m.AssessmentId == data.AssessmentAId)));
        }

        [Fact]
        public async Task Gradebook_ForAnotherLecturersAssessment_IsNotFound()
        {
            var response = await _factory.ClientFor("Lecturer", _factory.Data.LecturerAId)
                .GetAsync($"/Grading/Gradebook/{Id(_factory.Data.AssessmentBId)}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Students_CannotOpenGrading()
        {
            var response = await _factory.ClientFor("Student", _factory.Data.StudentId)
                .GetAsync($"/Grading/Gradebook/{Id(_factory.Data.AssessmentAId)}");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/AccessDenied", response.Headers.Location?.PathAndQuery);
        }
    }
}
