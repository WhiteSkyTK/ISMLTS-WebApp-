using System.Net;
using ISMLTS_WebApp_.Models;

namespace ISMLTS.Tests.Integration
{
    // Own factory: these tests add submissions and marks
    public class PortfolioPageTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public PortfolioPageTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Student_SeesTheirWork_ButNotUnreleasedMarks()
        {
            var data = _factory.Data;
            await _factory.WithDbAsync(async db =>
            {
                db.Submissions.Add(new Submission { AssessmentId = data.AssessmentAId, StudentId = data.StudentId, SubmittedAt = DateTime.UtcNow, Link = "https://github.com/student-a/poe" });
                db.Marks.Add(new Mark { StudentId = data.StudentId, ModuleId = data.ModuleAId, AssessmentId = data.AssessmentAId, AssessmentName = "POE A", Score = 88, MaxScore = 100, Feedback = "Secret until released" });
                return await db.SaveChangesAsync();
            });

            var html = await _factory.ClientFor("Student", data.StudentId).GetStringAsync("/Portfolio");

            Assert.Contains("POE A", html);
            Assert.Contains("https://github.com/student-a/poe", html);
            Assert.Contains("Waiting for marks", html);
            Assert.DoesNotContain("Secret until released", html);
        }

        [Fact]
        public async Task OtherStudents_DoNotSeeIt()
        {
            var html = await _factory.ClientFor("Student", _factory.Data.OtherStudentId).GetStringAsync("/Portfolio");

            Assert.DoesNotContain("https://github.com/student-a/poe", html);
            Assert.DoesNotContain(_factory.Data.ModuleACode, html);
        }

        [Fact]
        public async Task Lecturers_CannotOpenAPortfolio()
        {
            var response = await _factory.ClientFor("Lecturer", _factory.Data.LecturerAId).GetAsync("/Portfolio");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/AccessDenied", response.Headers.Location?.PathAndQuery);
        }
    }
}
