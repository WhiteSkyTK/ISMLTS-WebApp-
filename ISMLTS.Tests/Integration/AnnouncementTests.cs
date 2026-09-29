using System.Globalization;
using System.Net;
using ISMLTS_WebApp_.Models;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    // Own factory: these tests post announcements
    public class AnnouncementTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public AnnouncementTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static async Task<HttpResponseMessage> PostAnnouncementAsync(HttpClient client, params (string Key, string Value)[] fields)
        {
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(client, "/Announcements/Create");
            return await client.PostAsync("/Announcements/Create", new FormUrlEncodedContent(
                fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value))
                    .Prepend(new KeyValuePair<string, string>("__RequestVerificationToken", token))));
        }

        [Fact]
        public async Task Lecturer_CannotPostToAnotherLecturersModule()
        {
            var lecturer = _factory.ClientFor("Lecturer", _factory.Data.LecturerAId);

            var response = await PostAnnouncementAsync(lecturer,
                ("ModuleId", Id(_factory.Data.ModuleBId)), ("Title", "Sneaky"), ("Body", "Not my module"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Pick one of your modules.", await response.Content.ReadAsStringAsync());
            Assert.False(await _factory.WithDbAsync(db => db.Announcements.AnyAsync(a => a.Title == "Sneaky")));
        }

        [Fact]
        public async Task ModuleAnnouncement_ReachesItsStudentsOnly()
        {
            var data = _factory.Data;
            var posted = await PostAnnouncementAsync(_factory.ClientFor("Lecturer", data.LecturerAId),
                ("ModuleId", Id(data.ModuleAId)), ("Title", "Class moved to Lab 3"), ("Body", "See you there."));
            Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
            var id = await _factory.WithDbAsync(db => db.Announcements.Where(a => a.Title == "Class moved to Lab 3").Select(a => a.AnnouncementId).SingleAsync());

            var enrolled = _factory.ClientFor("Student", data.StudentId);
            Assert.Contains("Class moved to Lab 3", await enrolled.GetStringAsync("/"));
            Assert.Equal(HttpStatusCode.OK, (await enrolled.GetAsync($"/Announcements/Details/{Id(id)}")).StatusCode);
            Assert.True(await _factory.WithDbAsync(db => db.Notifications.AnyAsync(n => n.UserId == data.StudentId && n.Title == "Announcement: Class moved to Lab 3")));

            var other = _factory.ClientFor("Student", data.OtherStudentId);
            Assert.DoesNotContain("Class moved to Lab 3", await other.GetStringAsync("/"));
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/Announcements/Details/{Id(id)}")).StatusCode);
        }

        [Fact]
        public async Task AdminAnnouncementForLecturers_IsHiddenFromStudents()
        {
            var data = _factory.Data;
            var posted = await PostAnnouncementAsync(_factory.ClientFor("Admin", data.AdminId),
                ("Audience", AnnouncementAudiences.Lecturers), ("Title", "Staff meeting Friday"), ("Body", "Boardroom, 14:00."));
            Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
            var id = await _factory.WithDbAsync(db => db.Announcements.Where(a => a.Title == "Staff meeting Friday").Select(a => a.AnnouncementId).SingleAsync());

            Assert.Equal(HttpStatusCode.OK, (await _factory.ClientFor("Lecturer", data.LecturerBId).GetAsync($"/Announcements/Details/{Id(id)}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _factory.ClientFor("Student", data.StudentId).GetAsync($"/Announcements/Details/{Id(id)}")).StatusCode);
        }

        [Fact]
        public async Task OnlyTheAuthorCanDeleteAnAnnouncement()
        {
            var data = _factory.Data;
            var id = await _factory.WithDbAsync(async db =>
            {
                var a = new Announcement { Title = "Mine", Body = "x", ModuleId = data.ModuleAId, AuthorRole = Roles.Lecturer, AuthorId = data.LecturerAId, AuthorName = "Lecturer A" };
                db.Announcements.Add(a);
                await db.SaveChangesAsync();
                return a.AnnouncementId;
            });

            var otherLecturer = _factory.ClientFor("Lecturer", data.LecturerBId);
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(otherLecturer, "/Announcements/Create");
            var response = await otherLecturer.PostAsync($"/Announcements/Delete/{Id(id)}",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(await _factory.WithDbAsync(db => db.Announcements.AnyAsync(a => a.AnnouncementId == id)));
        }

        [Fact]
        public async Task StudentsCannotPostAnnouncements()
        {
            var response = await _factory.ClientFor("Student", _factory.Data.StudentId).GetAsync("/Announcements/Create");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/AccessDenied", response.Headers.Location?.PathAndQuery);
        }
    }
}
