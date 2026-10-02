using System.Net;
using System.Text.RegularExpressions;
using ISMLTS_WebApp_.Models;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    public class CalendarPageTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public CalendarPageTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task EveryRole_SeesTheirCalendar()
        {
            // The seeded assessments are due in 7 days
            var student = await _factory.ClientFor(Roles.Student, _factory.Data.StudentId).GetStringAsync("/Calendar");
            var lecturer = await _factory.ClientFor(Roles.Lecturer, _factory.Data.LecturerAId).GetStringAsync("/Calendar");
            var admin = await _factory.ClientFor(Roles.Admin, _factory.Data.AdminId).GetStringAsync("/Calendar");

            Assert.Contains($"{_factory.Data.ModuleACode}: POE A due", student);
            Assert.DoesNotContain(_factory.Data.ModuleBCode, student);
            Assert.Contains($"{_factory.Data.ModuleACode}: POE A due", lecturer);
            Assert.DoesNotContain(_factory.Data.ModuleBCode, lecturer);
            Assert.Contains($"{_factory.Data.ModuleBCode}: POE B due", admin);
            Assert.Contains("Get my calendar link", student);
            Assert.DoesNotContain("Get my calendar link", lecturer);
        }

        [Fact]
        public async Task TheFeedLink_IsShownOnce_WorksWithoutSigningIn_AndOnlyForItsStudent()
        {
            var student = _factory.ClientFor(Roles.Student, _factory.Data.StudentId);
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(student, "/Calendar");
            var created = await student.PostAsync("/Calendar/NewFeedLink", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
            var html = await created.Content.ReadAsStringAsync();
            var link = Regex.Match(html, "id=\"feedUrl\"[^>]*value=\"([^\"]+)\"", RegexOptions.None, TimeSpan.FromSeconds(1)).Groups[1].Value.Replace("&amp;", "&");
            Assert.StartsWith("https://localhost/Calendar/Feed?token=", link);

            var anonymous = _factory.ClientFor();
            var feed = await anonymous.GetAsync(link);
            var ics = await feed.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, feed.StatusCode);
            Assert.Equal("text/calendar", feed.Content.Headers.ContentType?.MediaType);
            Assert.Contains($"SUMMARY:{_factory.Data.ModuleACode}: POE A due", ics);
            Assert.DoesNotContain(_factory.Data.ModuleBCode, ics);

            // Only the hash is stored, and the page doesn't show the link again
            var stored = await _factory.WithDbAsync(db => db.Students.Where(s => s.StudentId == _factory.Data.StudentId).Select(s => s.CalendarTokenHash).SingleAsync());
            Assert.Equal(64, stored?.Length);
            Assert.DoesNotContain(link[(link.IndexOf('=') + 1)..], stored);
            Assert.DoesNotContain("feedUrl", await student.GetStringAsync("/Calendar"));

            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/Calendar/Feed?token=not-a-real-token")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/Calendar/Feed")).StatusCode);
        }
    }
}
