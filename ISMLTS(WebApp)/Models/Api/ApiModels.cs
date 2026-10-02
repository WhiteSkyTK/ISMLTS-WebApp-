using ISMLTS_WebApp_.Services;

namespace ISMLTS_WebApp_.Models.Api
{
    // What the Android app sends and gets back. JSON names are camelCase; due dates are plain dates ("2026-10-10")
    // and every other time is UTC with an offset ("2026-10-01T09:00:00+00:00").

    public record LoginRequest(string? Login, string? Password, string? Code);

    public record RefreshRequest(string? RefreshToken);

    public record TokenResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, StudentDto Student);

    public record StudentDto(int StudentId, string Name, string Email, string? Programme, bool TwoFactorEnabled);

    public record NextDueDto(int AssessmentId, string Name, DateOnly DueDate);

    public record ModuleDto(
        int ModuleId,
        string Code,
        string Name,
        string Term,
        string? Course,
        string? Lecturer,
        decimal? Average,
        int? AttendancePercent,
        int SessionsAttended,
        int SessionsHeld,
        int Submitted,
        int AssessmentCount,
        bool AtRisk,
        IReadOnlyList<string> RiskReasons,
        NextDueDto? NextDue);

    public record MarkDto(decimal Score, decimal MaxScore, decimal Percentage, string? Feedback);

    // The newest uploaded file counts; download it from /api/v1/files/{fileId}
    public record SubmissionFileDto(int FileId, string Name, long SizeBytes, DateTimeOffset UploadedAt);

    // Status is "not_submitted", "submitted" or "late"; Mark is null until the lecturer releases the marks.
    // LastDayToSubmit is null when late work is always accepted; SubmissionsOpen is false once it has passed.
    public record AssessmentDto(
        int AssessmentId,
        int ModuleId,
        string ModuleCode,
        string Name,
        string Type,
        string? Description,
        DateOnly DueDate,
        decimal MaxScore,
        string Status,
        DateTimeOffset? SubmittedAt,
        string? Link,
        bool MarksReleased,
        MarkDto? Mark,
        SubmissionFileDto? File,
        int FileCount,
        DateOnly? LastDayToSubmit,
        bool SubmissionsOpen);

    public record SubmissionRequest(string? Link);

    public record MarkListItemDto(int MarkId, int ModuleId, string ModuleCode, int? AssessmentId, string Name, decimal Score, decimal MaxScore, decimal Percentage, string? Feedback, DateOnly CapturedOn);

    public record AttendanceDto(int ModuleId, string ModuleCode, string ModuleName, int Attended, int Total, int Percentage);

    public record ScanRequest(string? Code, double? Latitude, double? Longitude, double? Accuracy);

    public record ScanResponse(string Result, string Message, string? ModuleCode);

    // Status is "open", "in_progress" or "resolved"
    public record TicketDto(int TicketId, int ModuleId, string ModuleCode, string Subject, string Description, string Status, string? Response, DateTimeOffset OpenedAt, DateTimeOffset? ResolvedAt);

    public record TicketRequest(int ModuleId, string? Subject, string? Description);

    public record NotificationDto(int NotificationId, string Title, string? Message, string? Url, DateTimeOffset CreatedAt, bool IsRead);

    public record NotificationPageDto(IReadOnlyList<NotificationDto> Items, int UnreadCount, int Page, int TotalPages);

    public record AnnouncementDto(int AnnouncementId, string Title, string Body, string? ModuleCode, string AuthorName, DateTimeOffset CreatedAt);

    public static class ApiMap
    {
        // Times are stored in UTC but read back from SQL Server without a kind
        public static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

        public static DateTimeOffset? Utc(DateTime? value) => value is DateTime v ? Utc(v) : null;

        public static string Status(string status) => status switch
        {
            "Late" => "late",
            "Submitted" => "submitted",
            "In Progress" => "in_progress",
            _ => status.ToLowerInvariant().Replace(' ', '_')
        };

        public static StudentDto Student(Student s) => new(s.StudentId, s.FullName, s.Email, s.Programme, s.TwoFactorEnabled);

        public static MarkDto? Mark(Mark? m) => m == null ? null : new MarkDto(m.Score, m.MaxScore, m.Percentage, m.Feedback);

        public static AssessmentDto Assessment(MyAssessmentRow r) => new(
            r.AssessmentId, r.ModuleId, r.ModuleCode, r.Name, r.Type, r.Description, DateOnly.FromDateTime(r.DueDate), r.MaxScore,
            Status(r.Status), Utc(r.SubmittedAt), r.Link, r.MarksReleased, Mark(r.Mark), File(r.File), r.FileCount,
            r.LastDay is DateTime last ? DateOnly.FromDateTime(last) : null, r.SubmissionsOpen);

        public static SubmissionFileDto? File(SubmissionFile? f) =>
            f == null ? null : new SubmissionFileDto(f.SubmissionFileId, f.FileName, f.SizeBytes, Utc(f.UploadedAt));

        public static ModuleDto Module(ModuleProgress p, Module? m) => new(
            p.ModuleId, p.Code, p.Name, m?.Term == "Term2" ? "Term 2" : "Term 1", m?.Course?.Code, m?.Lecturer?.FullName,
            p.Average, p.AttendancePercent, p.SessionsAttended, p.SessionsHeld, p.Submitted, p.AssessmentCount, p.AtRisk, p.RiskReasons,
            p.NextDue == null ? null : new NextDueDto(p.NextDue.AssessmentId, p.NextDue.Name, DateOnly.FromDateTime(p.NextDue.DueDate)));

        public static MarkListItemDto MarkItem(Mark m) => new(
            m.MarkId, m.ModuleId, m.Module?.Code ?? string.Empty, m.AssessmentId, m.AssessmentName, m.Score, m.MaxScore, m.Percentage, m.Feedback,
            DateOnly.FromDateTime(m.DateCaptured));

        public static AttendanceDto Attendance(MyAttendanceRow r) => new(r.ModuleId, r.ModuleCode, r.ModuleName, r.Attended, r.Total, r.Percentage);

        public static TicketDto Ticket(Ticket t) => new(
            t.TicketId, t.ModuleId, t.Module?.Code ?? string.Empty, t.Subject, t.Description, Status(t.Status), t.LecturerResponse,
            Utc(t.DateOpened), Utc(t.DateResolved));

        public static NotificationDto Notification(Notification n) => new(n.NotificationId, n.Title, n.Message, n.Url, Utc(n.CreatedAt), n.IsRead);

        public static AnnouncementDto Announcement(Announcement a) => new(a.AnnouncementId, a.Title, a.Body, a.Module?.Code, a.AuthorName, Utc(a.CreatedAt));
    }
}
