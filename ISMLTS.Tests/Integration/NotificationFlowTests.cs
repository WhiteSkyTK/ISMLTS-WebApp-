using System.Globalization;
using System.Net;
using ISMLTS_WebApp_.Models;
using Microsoft.EntityFrameworkCore;

namespace ISMLTS.Tests.Integration
{
    // Own factory: these tests create notifications
    public class NotificationFlowTests : IClassFixture<IsmltsFactory>
    {
        private readonly IsmltsFactory _factory;

        public NotificationFlowTests(IsmltsFactory factory)
        {
            _factory = factory;
        }

        private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, string token, params (string Key, string Value)[] fields) =>
            client.PostAsync(url, new FormUrlEncodedContent(
                fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value))
                    .Prepend(new KeyValuePair<string, string>("__RequestVerificationToken", token))));

        [Fact]
        public async Task TicketReply_LightsUpTheStudentsBell_AndSeeingItClearsTheBadge()
        {
            var data = _factory.Data;
            var lecturer = _factory.ClientFor("Lecturer", data.LecturerBId);
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(lecturer, $"/Tickets/Respond/{Id(data.TicketBId)}");
            var reply = await PostAsync(lecturer, $"/Tickets/Respond/{Id(data.TicketBId)}", token, ("status", "Resolved"), ("lecturerResponse", "See section 2."));
            Assert.Equal(HttpStatusCode.Redirect, reply.StatusCode);

            var student = _factory.ClientFor("Student", data.OtherStudentId);
            var dashboard = await student.GetStringAsync("/");
            Assert.Contains("data-notification-number>1<", dashboard);
            Assert.Contains("Reply to your ticket: Help", dashboard);

            var notificationId = await _factory.WithDbAsync(db => db.Notifications
                .Where(n => n.Role == Roles.Student && n.UserId == data.OtherStudentId).Select(n => n.NotificationId).SingleAsync());
            token = await IsmltsFactory.GetAntiforgeryTokenAsync(student, "/");
            var seen = await PostAsync(student, "/Notifications/MarkRead", token, ("ids", Id(notificationId)));
            Assert.Equal(HttpStatusCode.OK, seen.StatusCode);
            Assert.Contains("\"unread\":0", await seen.Content.ReadAsStringAsync());

            Assert.DoesNotContain("data-notification-count", await student.GetStringAsync("/"));
        }

        [Fact]
        public async Task OpeningANotification_FollowsItsLink_ButNotSomeoneElses()
        {
            var data = _factory.Data;
            var id = await _factory.WithDbAsync(async db =>
            {
                var n = new Notification { Role = Roles.Student, UserId = data.StudentId, Title = "New mark", Url = "/Marks/MyMarks" };
                db.Notifications.Add(n);
                await db.SaveChangesAsync();
                return n.NotificationId;
            });

            var intruder = _factory.ClientFor("Student", data.OtherStudentId);
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(intruder, "/");
            Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(intruder, $"/Notifications/Open/{Id(id)}", token)).StatusCode);

            var owner = _factory.ClientFor("Student", data.StudentId);
            token = await IsmltsFactory.GetAntiforgeryTokenAsync(owner, "/");
            var open = await PostAsync(owner, $"/Notifications/Open/{Id(id)}", token);
            Assert.Equal(HttpStatusCode.Redirect, open.StatusCode);
            Assert.Equal("/Marks/MyMarks", open.Headers.Location?.OriginalString);
            Assert.True(await _factory.WithDbAsync(db => db.Notifications.Where(n => n.NotificationId == id).Select(n => n.IsRead).SingleAsync()));
        }

        [Fact]
        public async Task NewAssessment_NotifiesStudentsInThatModuleOnly()
        {
            var data = _factory.Data;
            var lecturer = _factory.ClientFor("Lecturer", data.LecturerAId);
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(lecturer, $"/Assessments/Create?moduleId={Id(data.ModuleAId)}");
            var created = await PostAsync(lecturer, "/Assessments/Create", token,
                ("ModuleId", Id(data.ModuleAId)), ("Name", "Quiz 9"), ("Type", "Quiz"), ("DueDate", "2026-12-01"));
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

            var recipients = await _factory.WithDbAsync(db => db.Notifications
                .Where(n => n.Title == "New Quiz: Quiz 9").Select(n => n.UserId).ToListAsync());
            Assert.Equal(new[] { data.StudentId }, recipients);
        }

        [Fact]
        public async Task StartingAttendance_NotifiesStudents_WithoutTheSessionCode()
        {
            var data = _factory.Data;
            var lecturer = _factory.ClientFor("Lecturer", data.LecturerAId);
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(lecturer, $"/Attendance/ForModule?moduleId={Id(data.ModuleAId)}");
            var started = await PostAsync(lecturer, "/Attendance/Start", token, ("moduleId", Id(data.ModuleAId)));
            Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);

            var code = await _factory.WithDbAsync(db => db.AttendanceSessions.Where(s => s.ModuleId == data.ModuleAId).Select(s => s.Code).FirstAsync());
            var notification = await _factory.WithDbAsync(db => db.Notifications.SingleAsync(n => n.Title == $"Attendance is open for {data.ModuleACode}"));
            Assert.Equal(data.StudentId, notification.UserId);
            Assert.DoesNotContain(code, $"{notification.Title} {notification.Message} {notification.Url}");
        }

        [Fact]
        public async Task StudentCanTurnOffEmails()
        {
            var data = _factory.Data;
            var student = _factory.ClientFor("Student", data.StudentId);
            var token = await IsmltsFactory.GetAntiforgeryTokenAsync(student, "/Notifications/Settings");
            var saved = await PostAsync(student, "/Notifications/Settings", token, ("EmailEnabled", "false"), ("WeeklyDigest", "false"));
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

            var settings = await _factory.WithDbAsync(db => db.NotificationSettings.SingleAsync(s => s.Role == Roles.Student && s.UserId == data.StudentId));
            Assert.False(settings.EmailEnabled);
            Assert.False(settings.WeeklyDigest);
        }

        [Fact]
        public async Task AdminsHaveNoEmailSettingsPage()
        {
            var response = await _factory.ClientFor("Admin", _factory.Data.AdminId).GetAsync("/Notifications/Settings");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/AccessDenied", response.Headers.Location?.PathAndQuery);
        }
    }
}
