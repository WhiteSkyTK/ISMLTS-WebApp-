using System.Globalization;
using System.Net;
using ISMLTS_WebApp_.Models;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    public class TermsAndTimetableTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public TermsAndTimetableTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string formPage, string url, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = await IsmltsFactory.GetAntiforgeryTokenAsync(client, formPage);
            return await client.PostAsync(url, new FormUrlEncodedContent(fields));
        }

        [Fact]
        public async Task OnlyAdmins_ManageTerms()
        {
            var admin = _factory.ClientFor(Roles.Admin, _factory.Data.AdminId);
            var name = $"Term {Guid.NewGuid():N}"[..20];

            var created = await PostAsync(admin, "/Terms/Create", "/Terms/Create", new()
            {
                ["Name"] = name, ["Code"] = "Term2", ["StartDate"] = "2026-07-13", ["EndDate"] = "2026-11-20"
            });
            var backwards = await PostAsync(admin, "/Terms/Create", "/Terms/Create", new()
            {
                ["Name"] = name + "x", ["Code"] = "Term1", ["StartDate"] = "2026-07-13", ["EndDate"] = "2026-01-01"
            });

            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
            Assert.Equal(HttpStatusCode.OK, backwards.StatusCode);
            Assert.Contains("The last day must be on or after the first day.", await backwards.Content.ReadAsStringAsync());
            Assert.Contains(name, await admin.GetStringAsync("/Terms"));
            foreach (var other in new[] { _factory.ClientFor(Roles.Lecturer, _factory.Data.LecturerAId), _factory.ClientFor(Roles.Student, _factory.Data.StudentId) })
            {
                var response = await other.GetAsync("/Terms");
                Assert.StartsWith("/Account/AccessDenied", response.Headers.Location?.PathAndQuery);
            }
        }

        [Fact]
        public async Task Lecturer_TimetablesOnlyTheirOwnModules()
        {
            var lecturer = _factory.ClientFor(Roles.Lecturer, _factory.Data.LecturerAId);
            Dictionary<string, string> Slot(int moduleId) => new()
            {
                ["ModuleId"] = Id(moduleId), ["Day"] = "Wednesday", ["StartTime"] = "13:00", ["EndTime"] = "14:30", ["Venue"] = "Lab 2"
            };

            var own = await PostAsync(lecturer, "/Timetable", "/Timetable/Create", Slot(_factory.Data.ModuleAId));
            var other = await PostAsync(lecturer, "/Timetable", "/Timetable/Create", Slot(_factory.Data.ModuleBId));

            Assert.Equal(HttpStatusCode.Redirect, own.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
            var page = await lecturer.GetStringAsync("/Timetable");
            Assert.Contains("13:00&#x2013;14:30", page);
            Assert.Contains("Lab 2", page);
            Assert.False(await _factory.WithDbAsync(db => db.TimetableSlots.AnyAsync(s => s.ModuleId == _factory.Data.ModuleBId)));
        }

        [Fact]
        public async Task Lecturer_CanMarkTheirOwnSessionAsACancelledClass()
        {
            var sessionId = await _factory.WithDbAsync(async db =>
            {
                var session = new AttendanceSession { ModuleId = _factory.Data.ModuleAId, Code = "CXL" + Guid.NewGuid().ToString("N")[..3].ToUpperInvariant(), StartedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(15) };
                db.AttendanceSessions.Add(session);
                await db.SaveChangesAsync();
                return session.SessionId;
            });
            var page = $"/Attendance/ForModule?moduleId={Id(_factory.Data.ModuleAId)}";
            var fields = new Dictionary<string, string> { ["cancelled"] = "true" };

            var otherLecturer = await PostAsync(_factory.ClientFor(Roles.Lecturer, _factory.Data.LecturerBId), "/Attendance", $"/Attendance/SetCancelled/{Id(sessionId)}", fields);
            var owner = await PostAsync(_factory.ClientFor(Roles.Lecturer, _factory.Data.LecturerAId), page, $"/Attendance/SetCancelled/{Id(sessionId)}", fields);

            Assert.Equal(HttpStatusCode.NotFound, otherLecturer.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, owner.StatusCode);
            Assert.True(await _factory.WithDbAsync(db => db.AttendanceSessions.Where(s => s.SessionId == sessionId).Select(s => s.IsCancelled).SingleAsync()));
        }
    }
}
