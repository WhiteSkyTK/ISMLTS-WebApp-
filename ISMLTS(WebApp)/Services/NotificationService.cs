using System.Globalization;
using Microsoft.Extensions.Options;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    public record Recipient(string Role, int UserId);

    public interface INotificationService
    {
        Task NotifyAsync(IEnumerable<Recipient> recipients, string title, string? message, string? url);
        Task<int> CountUnreadAsync(string role, int userId);
        Task<List<Notification>> GetLatestAsync(string role, int userId, int take);
        Task<PagedList<Notification>> GetPageAsync(string role, int userId, bool unreadOnly, int page);
        Task<string?> OpenAsync(string role, int userId, int notificationId);
        Task<int> MarkReadAsync(string role, int userId, IReadOnlyCollection<int>? notificationIds);

        Task AssessmentPostedAsync(Assessment assessment, Module module);
        Task MarkSavedAsync(Mark mark, Module module, bool updated);
        Task TicketRaisedAsync(Ticket ticket, Module module, string studentName);
        Task TicketAnsweredAsync(Ticket ticket, Module module);
        Task AttendanceOpenedAsync(Module module);
        Task MarkedPresentAsync(Module module, int studentId);
        Task ScanRemovedAsync(Module module, int studentId);
        Task AnnouncementPostedAsync(Announcement announcement, Module? module);
    }

    public class NotificationService : INotificationService
    {
        public const string ListUrl = "/Notifications";

        private readonly INotificationRepository _notifications;
        private readonly INotificationSettingRepository _settings;
        private readonly IStudentRepository _students;
        private readonly ILecturerRepository _lecturers;
        private readonly IEmailSender _email;
        private readonly EmailOptions _emailOptions;

        public NotificationService(
            INotificationRepository notifications,
            INotificationSettingRepository settings,
            IStudentRepository students,
            ILecturerRepository lecturers,
            IEmailSender email,
            IOptions<EmailOptions> emailOptions)
        {
            _notifications = notifications;
            _settings = settings;
            _students = students;
            _lecturers = lecturers;
            _email = email;
            _emailOptions = emailOptions.Value;
        }

        // ---------- Core ----------

        public async Task NotifyAsync(IEnumerable<Recipient> recipients, string title, string? message, string? url)
        {
            var safeUrl = LocalUrl.IsLocal(url) ? url : null;
            var rows = recipients.Distinct().Select(r => new Notification
            {
                Role = r.Role,
                UserId = r.UserId,
                Title = Truncate(title, 150),
                Message = message == null ? null : Truncate(message, 500),
                Url = safeUrl,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            if (rows.Count == 0) return;
            await _notifications.AddRangeAsync(rows);
            await _notifications.SaveChangesAsync();
        }

        public Task<int> CountUnreadAsync(string role, int userId) => _notifications.CountUnreadAsync(role, userId);

        public Task<List<Notification>> GetLatestAsync(string role, int userId, int take) => _notifications.GetLatestAsync(role, userId, take);

        public Task<PagedList<Notification>> GetPageAsync(string role, int userId, bool unreadOnly, int page) =>
            _notifications.GetPageAsync(role, userId, unreadOnly, page);

        // Marks it read and returns where to go next; null when it isn't this user's notification
        public async Task<string?> OpenAsync(string role, int userId, int notificationId)
        {
            var notification = await _notifications.GetForUserAsync(role, userId, notificationId);
            if (notification == null) return null;

            await _notifications.MarkReadAsync(role, userId, new[] { notificationId });
            return LocalUrl.IsLocal(notification.Url) ? notification.Url : ListUrl;
        }

        public Task<int> MarkReadAsync(string role, int userId, IReadOnlyCollection<int>? notificationIds) =>
            _notifications.MarkReadAsync(role, userId, notificationIds);

        // ---------- Triggers ----------

        public async Task AssessmentPostedAsync(Assessment assessment, Module module) =>
            await NotifyAsync(await StudentsOfAsync(module),
                $"New {assessment.Type}: {assessment.Name}",
                $"{module.Code} · due {assessment.DueDate.ToString("ddd dd MMM", CultureInfo.InvariantCulture)}",
                "/Assessments/MyAssessments");

        public async Task MarkSavedAsync(Mark mark, Module module, bool updated)
        {
            var score = $"{Number(mark.Score)}/{Number(mark.MaxScore)} ({Number(mark.Percentage)}%)";
            await NotifyAsync(new[] { new Recipient(Roles.Student, mark.StudentId) },
                updated ? $"Mark updated: {mark.AssessmentName}" : $"New mark: {mark.AssessmentName}",
                $"{module.Code} · {score}",
                "/Marks/MyMarks");
        }

        public async Task TicketRaisedAsync(Ticket ticket, Module module, string studentName)
        {
            var url = $"/Tickets/Respond/{ticket.TicketId}";
            await NotifyAsync(new[] { new Recipient(Roles.Lecturer, module.LecturerId) },
                $"New ticket from {studentName}",
                $"{module.Code} · {ticket.Subject}",
                url);

            var lecturer = await _lecturers.GetByIdAsync(module.LecturerId);
            await EmailAsync(Roles.Lecturer, module.LecturerId, lecturer?.Email,
                $"New ticket in {module.Code}: {ticket.Subject}",
                $"{studentName} asked a question in {module.Code}:\n\n{ticket.Description}\n\nReply here: {_emailOptions.Link(url)}");
        }

        public async Task TicketAnsweredAsync(Ticket ticket, Module module)
        {
            const string url = "/Tickets/MyTickets";
            await NotifyAsync(new[] { new Recipient(Roles.Student, ticket.StudentId) },
                $"Reply to your ticket: {ticket.Subject}",
                $"{module.Code} · {ticket.Status}",
                url);

            var student = await _students.GetByIdAsync(ticket.StudentId);
            await EmailAsync(Roles.Student, ticket.StudentId, student?.Email,
                $"Your {module.Code} lecturer replied: {ticket.Subject}",
                $"Your ticket is now: {ticket.Status}\n\n{ticket.LecturerResponse}\n\nSee it here: {_emailOptions.Link(url)}");
        }

        // Never include the session code: it would let students mark themselves present from home
        public async Task AttendanceOpenedAsync(Module module) =>
            await NotifyAsync(await StudentsOfAsync(module),
                $"Attendance is open for {module.Code}",
                "Scan the QR code in class to mark yourself present.",
                "/Attendance/Scan");

        public Task MarkedPresentAsync(Module module, int studentId) =>
            NotifyAsync(new[] { new Recipient(Roles.Student, studentId) },
                $"You were marked present in {module.Code}",
                "Your lecturer marked you present for this session.",
                "/Attendance/MyAttendance");

        public Task ScanRemovedAsync(Module module, int studentId) =>
            NotifyAsync(new[] { new Recipient(Roles.Student, studentId) },
                $"Your {module.Code} attendance scan was removed",
                "Your lecturer removed your scan for this session. Ask them if you think it's a mistake.",
                "/Attendance/MyAttendance");

        public async Task AnnouncementPostedAsync(Announcement announcement, Module? module)
        {
            var recipients = new List<Recipient>();
            if (module != null)
            {
                recipients.AddRange(await StudentsOfAsync(module));
            }
            else
            {
                if (announcement.Audience is AnnouncementAudiences.Everyone or AnnouncementAudiences.Students)
                    recipients.AddRange((await _students.GetAllAsync()).Select(s => new Recipient(Roles.Student, s.StudentId)));
                if (announcement.Audience is AnnouncementAudiences.Everyone or AnnouncementAudiences.Lecturers)
                    recipients.AddRange((await _lecturers.GetAllAsync()).Select(l => new Recipient(Roles.Lecturer, l.LecturerId)));
            }

            var author = new Recipient(announcement.AuthorRole, announcement.AuthorId);
            await NotifyAsync(recipients.Where(r => r != author),
                $"Announcement: {announcement.Title}",
                module != null ? $"{module.Code} · {announcement.AuthorName}" : announcement.AuthorName,
                $"/Announcements/Details/{announcement.AnnouncementId}");
        }

        // ---------- Helpers ----------

        private async Task<IEnumerable<Recipient>> StudentsOfAsync(Module module) =>
            (await _students.GetByModuleAsync(module.ModuleId)).Select(s => new Recipient(Roles.Student, s.StudentId));

        // Only when email is configured and the person hasn't switched it off (no settings row = on)
        private async Task EmailAsync(string role, int userId, string? address, string subject, string body)
        {
            if (!_email.IsEnabled || string.IsNullOrWhiteSpace(address)) return;

            var settings = await _settings.GetAsync(role, userId);
            if (settings is { EmailEnabled: false }) return;

            await _email.SendAsync(address, subject, body);
        }

        private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
    }
}
