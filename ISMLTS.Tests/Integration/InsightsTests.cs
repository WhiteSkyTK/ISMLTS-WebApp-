using System.Net;
using ISMLTS_WebApp_.Models;

namespace ISMLTS.Tests.Integration
{
    // Own factory: these tests add marks and attendance
    public class InsightsTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public InsightsTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task MyProgress_CountsReleasedMarksOnly_AndShowsAttendance()
        {
            var data = _factory.Data;
            await _factory.WithDbAsync(async db =>
            {
                var session = new AttendanceSession { ModuleId = data.ModuleAId, Code = "PROG01", StartedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow };
                db.AttendanceSessions.AddRange(session,
                    new AttendanceSession { ModuleId = data.ModuleAId, Code = "PROG02", StartedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow });
                db.AttendanceRecords.Add(new AttendanceRecord { Session = session, StudentId = data.StudentId, ScannedAt = DateTime.UtcNow });
                db.Marks.Add(new Mark { StudentId = data.StudentId, ModuleId = data.ModuleAId, AssessmentName = "Class test", Score = 30, MaxScore = 100 });
                db.Marks.Add(new Mark { StudentId = data.StudentId, ModuleId = data.ModuleAId, AssessmentId = data.AssessmentAId, AssessmentName = "POE A", Score = 100, MaxScore = 100 });
                return await db.SaveChangesAsync();
            });

            var html = await _factory.ClientFor("Student", data.StudentId).GetStringAsync("/Insights/MyProgress");

            Assert.Contains("30%", html);                               // the unreleased 100% doesn't count yet
            Assert.Contains("Average 30% is below 50%", html);
            Assert.Contains("Attendance 50% is below 75%", html);
            Assert.Contains("(1 of 2)", html);
        }

        [Fact]
        public async Task MyProgress_IsForStudentsOnly()
        {
            var response = await _factory.ClientFor("Lecturer", _factory.Data.LecturerAId).GetAsync("/Insights/MyProgress");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/AccessDenied", response.Headers.Location?.PathAndQuery);
        }
    }
}
