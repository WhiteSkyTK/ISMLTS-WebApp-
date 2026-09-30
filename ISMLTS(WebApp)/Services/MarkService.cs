using System.Globalization;
using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    // One set of rules for every place marks are entered (form, gradebook, Quick Eval, CSV import)
    public static class MarkRules
    {
        public const int MaxFeedbackLength = 1000;
        public const decimal HighestTotal = 1000;

        public static string? CheckOutOf(decimal? outOf) => outOf switch
        {
            null => "Enter the total it's out of.",
            <= 0 => "The total must be more than 0.",
            > HighestTotal => $"The total can't be more than {Format(HighestTotal)}.",
            _ => null
        };

        public static string? CheckScore(decimal? score, decimal outOf) => score switch
        {
            null => "Enter a score.",
            < 0 => "The score can't be negative.",
            _ when score > outOf => $"The score can't be more than {Format(outOf)}.",
            _ => null
        };

        public static string? CheckFeedback(string? feedback) =>
            feedback?.Length > MaxFeedbackLength ? $"Keep feedback to {MaxFeedbackLength} characters." : null;

        public static string Format(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public record Grader(int LecturerId, string Name);

    // Everything needed to record one student's mark; Assessment is null for work that isn't a listed assessment
    public record MarkEntry(int StudentId, Assessment? Assessment, string Name, decimal Score, decimal OutOf, string? Feedback, DateTime DateCaptured);

    public interface IMarkService
    {
        Task<Mark> SaveAsync(Module module, MarkEntry entry, Grader grader, string source, Mark? existing = null);
        Task DeleteAsync(Mark mark, Grader grader);
        Task SetReleasedAsync(Assessment assessment, Module module, bool released);
        Task DeleteAssessmentAsync(Assessment assessment);
        Task<List<MarkChange>> HistoryAsync(int markId);
    }

    public class MarkService : IMarkService
    {
        private readonly IMarkRepository _marks;
        private readonly IMarkChangeRepository _changes;
        private readonly IAssessmentRepository _assessments;
        private readonly INotificationService _notifications;

        public MarkService(IMarkRepository marks, IMarkChangeRepository changes, IAssessmentRepository assessments, INotificationService notifications)
        {
            _marks = marks;
            _changes = changes;
            _assessments = assessments;
            _notifications = notifications;
        }

        // Creates the mark, or updates it when the student already has one for this assessment (or `existing` is given).
        // Every real change is audited; the student is only notified once they can see the mark.
        public async Task<Mark> SaveAsync(Module module, MarkEntry entry, Grader grader, string source, Mark? existing = null)
        {
            var mark = existing ?? (entry.Assessment != null
                ? await _marks.GetByAssessmentAndStudentAsync(entry.Assessment.AssessmentId, entry.StudentId)
                : null);
            var isNew = mark == null;
            mark ??= new Mark { StudentId = entry.StudentId, ModuleId = module.ModuleId };

            var feedback = string.IsNullOrWhiteSpace(entry.Feedback) ? null : entry.Feedback.Trim();
            var change = new MarkChange
            {
                StudentId = entry.StudentId,
                ModuleId = module.ModuleId,
                AssessmentName = entry.Assessment?.Name ?? entry.Name,
                Action = isNew ? MarkChangeActions.Created : MarkChangeActions.Updated,
                OldScore = isNew ? null : mark.Score,
                OldMaxScore = isNew ? null : mark.MaxScore,
                NewScore = entry.Score,
                NewMaxScore = entry.OutOf,
                FeedbackChanged = isNew ? feedback != null : mark.Feedback != feedback,
                Source = source,
                ChangedById = grader.LecturerId,
                ChangedByName = grader.Name
            };

            var unchanged = !isNew && mark.Score == entry.Score && mark.MaxScore == entry.OutOf && !change.FeedbackChanged
                && mark.AssessmentId == entry.Assessment?.AssessmentId && mark.AssessmentName == change.AssessmentName
                && mark.DateCaptured.Date == entry.DateCaptured.Date;
            if (unchanged) return mark;

            mark.AssessmentId = entry.Assessment?.AssessmentId;
            mark.AssessmentName = change.AssessmentName;
            mark.Score = entry.Score;
            mark.MaxScore = entry.OutOf;
            mark.Feedback = feedback;
            mark.DateCaptured = entry.DateCaptured.Date;

            if (isNew) await _marks.AddAsync(mark);
            await _marks.SaveChangesAsync();

            change.MarkId = mark.MarkId;
            await _changes.AddAsync(change);
            await _changes.SaveChangesAsync();

            if (entry.Assessment == null || entry.Assessment.MarksReleased)
            {
                await _notifications.MarkSavedAsync(mark, module, updated: !isNew);
            }
            return mark;
        }

        public async Task DeleteAsync(Mark mark, Grader grader)
        {
            await _changes.AddAsync(new MarkChange
            {
                MarkId = mark.MarkId,
                StudentId = mark.StudentId,
                ModuleId = mark.ModuleId,
                AssessmentName = mark.AssessmentName,
                Action = MarkChangeActions.Deleted,
                OldScore = mark.Score,
                OldMaxScore = mark.MaxScore,
                FeedbackChanged = mark.Feedback != null,
                Source = MarkSources.Form,
                ChangedById = grader.LecturerId,
                ChangedByName = grader.Name
            });
            _marks.Delete(mark);
            await _marks.SaveChangesAsync();
        }

        // Releasing tells every student who has a mark for the assessment; hiding again just hides it
        public async Task SetReleasedAsync(Assessment assessment, Module module, bool released)
        {
            if (assessment.MarksReleased == released) return;

            assessment.MarksReleased = released;
            assessment.MarksReleasedAt = released ? DateTime.UtcNow : null;
            _assessments.Update(assessment);
            await _assessments.SaveChangesAsync();

            if (released)
            {
                await _notifications.MarksReleasedAsync(assessment, module, await _marks.GetByAssessmentAsync(assessment.AssessmentId));
            }
        }

        // Marks outlive their assessment: they're unlinked (keeping their name) before it's deleted
        public async Task DeleteAssessmentAsync(Assessment assessment)
        {
            await _marks.UnlinkAssessmentAsync(assessment.AssessmentId);
            _assessments.Delete(assessment);
            await _assessments.SaveChangesAsync();
        }

        public Task<List<MarkChange>> HistoryAsync(int markId) => _changes.GetByMarkAsync(markId);
    }
}
