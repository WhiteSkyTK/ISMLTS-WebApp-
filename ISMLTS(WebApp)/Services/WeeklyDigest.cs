using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    public record DigestItem(string ModuleCode, string Name, DateTime DueDate);

    public static class WeeklyDigest
    {
        // South Africa Standard Time has no daylight saving, so a fixed offset is exact
        private static readonly TimeSpan SouthAfrica = TimeSpan.FromHours(2);

        // Monday from 07:00 SAST, at most once a week per student
        public static bool IsDue(DateTime nowUtc, DateTime? lastSentUtc)
        {
            var local = nowUtc + SouthAfrica;
            if (local.DayOfWeek != DayOfWeek.Monday || local.Hour < 7) return false;
            return lastSentUtc == null || nowUtc - lastSentUtc.Value > TimeSpan.FromDays(6);
        }

        public static DateTime TodayInSouthAfrica(DateTime nowUtc) => (nowUtc + SouthAfrica).Date;

        public static string Build(string studentName, IReadOnlyList<DigestItem> items, string link)
        {
            var text = new StringBuilder();
            text.AppendLine(CultureInfo.InvariantCulture, $"Hi {studentName},");
            text.AppendLine();
            text.AppendLine(items.Count == 1 ? "You have 1 assessment due this week:" : $"You have {items.Count} assessments due this week:");
            foreach (var item in items.OrderBy(i => i.DueDate))
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"- {item.DueDate.ToString("ddd dd MMM", CultureInfo.InvariantCulture)}: {item.Name} ({item.ModuleCode})");
            }
            text.AppendLine();
            text.AppendLine(CultureInfo.InvariantCulture, $"Submit your work here: {link}");
            text.AppendLine();
            text.AppendLine("You can switch these emails off under Notification settings.");
            return text.ToString();
        }
    }

    public interface IWeeklyDigestSender
    {
        Task<int> SendDueAsync(DateTime nowUtc);
    }

    public class WeeklyDigestSender : IWeeklyDigestSender
    {
        private readonly IStudentRepository _students;
        private readonly IAssessmentRepository _assessments;
        private readonly ISubmissionRepository _submissions;
        private readonly INotificationSettingRepository _settings;
        private readonly IEmailSender _email;
        private readonly EmailOptions _emailOptions;

        public WeeklyDigestSender(
            IStudentRepository students,
            IAssessmentRepository assessments,
            ISubmissionRepository submissions,
            INotificationSettingRepository settings,
            IEmailSender email,
            IOptions<EmailOptions> emailOptions)
        {
            _students = students;
            _assessments = assessments;
            _submissions = submissions;
            _settings = settings;
            _email = email;
            _emailOptions = emailOptions.Value;
        }

        // Emails every student who has unsubmitted work due in the next 7 days; returns how many were sent
        public async Task<int> SendDueAsync(DateTime nowUtc)
        {
            if (!_email.IsEnabled) return 0;

            var today = WeeklyDigest.TodayInSouthAfrica(nowUtc);
            var dueThisWeek = await _assessments.GetDueBetweenAsync(today, today.AddDays(7));
            if (dueThisWeek.Count == 0) return 0;

            var settings = await _settings.GetForRoleAsync(Roles.Student);
            var sent = 0;
            foreach (var student in await _students.GetAllWithModulesAsync())
            {
                settings.TryGetValue(student.StudentId, out var setting);
                if (setting is { WeeklyDigest: false } || !WeeklyDigest.IsDue(nowUtc, setting?.LastDigestSentAt)) continue;

                var submitted = (await _submissions.GetByStudentAsync(student.StudentId)).Select(s => s.AssessmentId).ToHashSet();
                var moduleIds = student.Modules.Select(m => m.ModuleId).ToHashSet();
                var items = dueThisWeek
                    .Where(a => moduleIds.Contains(a.ModuleId) && !submitted.Contains(a.AssessmentId))
                    .Select(a => new DigestItem(a.Module?.Code ?? string.Empty, a.Name, a.DueDate))
                    .ToList();
                if (items.Count == 0) continue;

                await _email.SendAsync(student.Email, "Due this week",
                    WeeklyDigest.Build(student.FullName, items, _emailOptions.Link("/Assessments/MyAssessments")));

                if (setting == null)
                {
                    setting = new NotificationSetting { Role = Roles.Student, UserId = student.StudentId };
                    await _settings.AddAsync(setting);
                    settings[student.StudentId] = setting;
                }
                setting.LastDigestSentAt = nowUtc;
                sent++;
            }

            await _settings.SaveChangesAsync();
            return sent;
        }
    }

    // Wakes every 30 minutes; WeeklyDigest.IsDue decides who actually gets an email (Monday mornings)
    public class WeeklyDigestService : BackgroundService
    {
        private static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(30);

        private readonly IServiceScopeFactory _scopes;
        private readonly IEmailSender _email;
        private readonly ILogger<WeeklyDigestService> _logger;

        public WeeklyDigestService(IServiceScopeFactory scopes, IEmailSender email, ILogger<WeeklyDigestService> logger)
        {
            _scopes = scopes;
            _email = email;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_email.IsEnabled) return;

            using var timer = new PeriodicTimer(CheckEvery);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                using var scope = _scopes.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<IWeeklyDigestSender>().SendDueAsync(DateTime.UtcNow);
                if (sent > 0)
                {
                    _logger.LogInformation("Sent {Count} weekly digest emails", sent);
                }
            }
        }
    }
}
