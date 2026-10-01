using System.Globalization;
using System.Net;
using ISMLTS_WebApp_.Models;

namespace ISMLTS.Tests.Integration
{
    public class ExportTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public ExportTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);

        [Fact]
        public async Task MarksExport_DownloadsACsv_ForTheModulesLecturerOnly()
        {
            var data = _factory.Data;

            var response = await _factory.ClientFor("Lecturer", data.LecturerBId).GetAsync($"/Marks/Export?moduleId={Id(data.ModuleBId)}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
            var csv = await response.Content.ReadAsStringAsync();
            Assert.Contains("Student,Email,Assessment,Score,Out of", csv);
            Assert.Contains("Student B,t@rcconnect.edu.za,Quiz,40,50,80", csv);

            var intruder = await _factory.ClientFor("Lecturer", data.LecturerAId).GetAsync($"/Marks/Export?moduleId={Id(data.ModuleBId)}");
            Assert.Equal(HttpStatusCode.NotFound, intruder.StatusCode);
        }

        [Fact]
        public async Task RegisterExport_ListsAbsentStudentsToo()
        {
            var data = _factory.Data;
            var sessionId = await _factory.WithDbAsync(async db =>
            {
                var session = new AttendanceSession { ModuleId = data.ModuleAId, Code = "EXPO01", StartedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow };
                db.AttendanceSessions.Add(session);
                await db.SaveChangesAsync();
                return session.SessionId;
            });

            var csv = await _factory.ClientFor("Lecturer", data.LecturerAId).GetStringAsync($"/Attendance/Export/{Id(sessionId)}");

            Assert.Contains("Student A,s@rcconnect.edu.za,Absent", csv);
        }
    }
}
