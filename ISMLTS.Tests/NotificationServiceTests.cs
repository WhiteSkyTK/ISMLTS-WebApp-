using ISMLTS_WebApp_.Data;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ISMLTS.Tests
{
    public class NotificationServiceTests : IDisposable
    {
        private readonly SqliteTestDb _db = new();
        private readonly FakeEmailSender _email = new();
        private int _lecturerId, _moduleId, _enrolledId, _otherStudentId;

        public NotificationServiceTests()
        {
            using var context = _db.NewContext();
            var lecturer = new Lecturer { FullName = "Lecturer", Email = "lecturer@lecturers.test", PasswordHash = "x" };
            var module = new Module { Code = "XADAD7112", Name = "WIL", Lecturer = lecturer };
            var enrolled = new Student { FullName = "Enrolled Student", Email = "enrolled@students.test", Modules = { module } };
            var other = new Student { FullName = "Other Student", Email = "other@students.test" };
            context.AddRange(lecturer, module, enrolled, other);
            context.SaveChanges();
            (_lecturerId, _moduleId, _enrolledId, _otherStudentId) = (lecturer.LecturerId, module.ModuleId, enrolled.StudentId, other.StudentId);
        }

        private NotificationService Service(ApplicationDbContext context) => new(
            new NotificationRepository(context),
            new NotificationSettingRepository(context),
            new StudentRepository(context),
            new LecturerRepository(context),
            _email,
            Options.Create(new EmailOptions { SiteUrl = "https://ismlts.example" }));

        private async Task<List<Notification>> AllNotificationsAsync()
        {
            await using var context = _db.NewContext();
            return await context.Notifications.ToListAsync();
        }

        [Fact]
        public async Task AssessmentPosted_NotifiesOnlyStudentsEnrolledInTheModule()
        {
            await using (var context = _db.NewContext())
            {
                var module = await context.Modules.FindAsync(_moduleId) ?? throw new InvalidOperationException();
                await Service(context).AssessmentPostedAsync(new Assessment { Name = "POE Part 1", Type = "POE", DueDate = new DateTime(2026, 10, 9) }, module);
            }

            var notification = Assert.Single(await AllNotificationsAsync());
            Assert.Equal((Roles.Student, _enrolledId), (notification.Role, notification.UserId));
            Assert.Equal("New POE: POE Part 1", notification.Title);
            Assert.Equal("XADAD7112 · due Fri 09 Oct", notification.Message);
        }

        [Fact]
        public async Task AttendanceOpened_TellsStudentsToScanInClassWithoutAnyCode()
        {
            await using (var context = _db.NewContext())
            {
                var module = await context.Modules.FindAsync(_moduleId) ?? throw new InvalidOperationException();
                await Service(context).AttendanceOpenedAsync(module);
            }

            var notification = Assert.Single(await AllNotificationsAsync());
            Assert.Equal("Attendance is open for XADAD7112", notification.Title);
            Assert.Equal("Scan the QR code in class to mark yourself present.", notification.Message);
            Assert.Equal("/Attendance/Scan", notification.Url);
        }

        [Fact]
        public async Task Notify_DropsLinksThatLeaveTheSite()
        {
            await using (var context = _db.NewContext())
            {
                await Service(context).NotifyAsync(new[] { new Recipient(Roles.Student, _enrolledId) }, "Hello", null, "https://evil.example");
            }

            Assert.Null(Assert.Single(await AllNotificationsAsync()).Url);
        }

        [Fact]
        public async Task Open_MarksReadAndReturnsTheLink_ButOnlyForItsOwner()
        {
            await using (var context = _db.NewContext())
            {
                await Service(context).NotifyAsync(new[] { new Recipient(Roles.Student, _enrolledId) }, "New mark", null, "/Marks/MyMarks");
            }
            var id = Assert.Single(await AllNotificationsAsync()).NotificationId;

            await using (var context = _db.NewContext())
            {
                Assert.Null(await Service(context).OpenAsync(Roles.Student, _otherStudentId, id));
                Assert.Null(await Service(context).OpenAsync(Roles.Lecturer, _enrolledId, id));
            }
            Assert.False(Assert.Single(await AllNotificationsAsync()).IsRead);

            await using (var context = _db.NewContext())
            {
                Assert.Equal("/Marks/MyMarks", await Service(context).OpenAsync(Roles.Student, _enrolledId, id));
            }
            Assert.True(Assert.Single(await AllNotificationsAsync()).IsRead);
        }

        [Fact]
        public async Task MarkRead_OnlyTouchesTheCallersOwnNotifications()
        {
            await using (var context = _db.NewContext())
            {
                var service = Service(context);
                await service.NotifyAsync(new[] { new Recipient(Roles.Student, _enrolledId), new Recipient(Roles.Student, _otherStudentId) }, "Hello", null, null);
                var all = await AllNotificationsAsync();

                var marked = await service.MarkReadAsync(Roles.Student, _enrolledId, all.Select(n => n.NotificationId).ToList());
                Assert.Equal(1, marked);
                Assert.Equal(0, await service.CountUnreadAsync(Roles.Student, _enrolledId));
                Assert.Equal(1, await service.CountUnreadAsync(Roles.Student, _otherStudentId));
            }
        }

        [Fact]
        public async Task TicketAnswered_EmailsTheStudentUnlessTheyTurnedEmailOff()
        {
            var ticket = new Ticket { StudentId = _enrolledId, ModuleId = _moduleId, Subject = "POE brief", Status = "Resolved", LecturerResponse = "See section 2." };

            await using (var context = _db.NewContext())
            {
                var module = await context.Modules.FindAsync(_moduleId) ?? throw new InvalidOperationException();
                await Service(context).TicketAnsweredAsync(ticket, module);
            }
            var email = Assert.Single(_email.Sent);
            Assert.Equal("enrolled@students.test", email.To);
            Assert.Contains("See section 2.", email.Body);
            Assert.Contains("https://ismlts.example/Tickets/MyTickets", email.Body);

            await using (var context = _db.NewContext())
            {
                context.NotificationSettings.Add(new NotificationSetting { Role = Roles.Student, UserId = _enrolledId, EmailEnabled = false });
                await context.SaveChangesAsync();
                var module = await context.Modules.FindAsync(_moduleId) ?? throw new InvalidOperationException();
                await Service(context).TicketAnsweredAsync(ticket, module);
            }
            Assert.Single(_email.Sent);
            Assert.Equal(2, (await AllNotificationsAsync()).Count);
        }

        [Fact]
        public async Task TicketRaised_NotifiesAndEmailsTheModuleLecturer()
        {
            await using (var context = _db.NewContext())
            {
                var module = await context.Modules.FindAsync(_moduleId) ?? throw new InvalidOperationException();
                await Service(context).TicketRaisedAsync(new Ticket { TicketId = 42, Subject = "Help", Description = "Question" }, module, "Enrolled Student");
            }

            var notification = Assert.Single(await AllNotificationsAsync());
            Assert.Equal((Roles.Lecturer, _lecturerId, "/Tickets/Respond/42"), (notification.Role, notification.UserId, notification.Url));
            Assert.Equal("lecturer@lecturers.test", Assert.Single(_email.Sent).To);
        }

        [Fact]
        public async Task CollegeWideAnnouncement_ForEveryone_ReachesStudentsAndLecturersButNotTheAuthor()
        {
            await using (var context = _db.NewContext())
            {
                var announcement = new Announcement { AnnouncementId = 3, Title = "Campus closed", Audience = AnnouncementAudiences.Everyone, AuthorRole = Roles.Lecturer, AuthorId = _lecturerId, AuthorName = "Lecturer" };
                await Service(context).AnnouncementPostedAsync(announcement, null);
            }

            var recipients = (await AllNotificationsAsync()).Select(n => (n.Role, n.UserId)).OrderBy(r => r.UserId).ToList();
            Assert.Equal(new[] { (Roles.Student, _enrolledId), (Roles.Student, _otherStudentId) }.OrderBy(r => r.Item2), recipients);
        }

        public void Dispose() => _db.Dispose();
    }
}
