using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Services
{
    // One piece of work in a student's portfolio: what they handed in and, once released, the mark and feedback
    public record PortfolioEntry(
        string Name,
        string Type,
        DateTime? DueDate,
        DateTime? SubmittedAt,
        string Status,
        string? Link,
        Mark? Mark,
        bool WaitingForMarks);

    public record PortfolioModule(int ModuleId, string Code, string Name, string Term, List<PortfolioEntry> Entries, decimal? Average);

    public static class Portfolio
    {
        // Everything a student handed in or got a released mark for, grouped by module, newest first.
        // Marks stay hidden until their assessment is released; links that aren't http(s) are dropped.
        public static List<PortfolioModule> Build(
            IEnumerable<Module> modules,
            IEnumerable<Assessment> assessments,
            IEnumerable<Submission> submissions,
            IEnumerable<Mark> marks)
        {
            var submissionByAssessment = submissions.Where(s => s.SubmittedAt != null)
                .GroupBy(s => s.AssessmentId).ToDictionary(g => g.Key, g => g.First());
            var visibleMarks = marks.Where(m => m.IsVisibleToStudent).ToList();
            var markByAssessment = visibleMarks.Where(m => m.AssessmentId != null)
                .GroupBy(m => m.AssessmentId!.Value).ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.MarkId).First());
            var assessmentsByModule = assessments.ToLookup(a => a.ModuleId);

            return modules.OrderBy(m => m.Term).ThenBy(m => m.Code).Select(module =>
            {
                var entries = assessmentsByModule[module.ModuleId]
                    .Where(a => submissionByAssessment.ContainsKey(a.AssessmentId) || markByAssessment.ContainsKey(a.AssessmentId))
                    .Select(a =>
                    {
                        var submission = submissionByAssessment.GetValueOrDefault(a.AssessmentId);
                        var mark = markByAssessment.GetValueOrDefault(a.AssessmentId);
                        return new PortfolioEntry(
                            a.Name,
                            a.Type,
                            a.DueDate,
                            submission?.SubmittedAt,
                            Submission.StatusFor(submission?.SubmittedAt, a.DueDate),
                            LinkValidator.IsWebLink(submission?.Link) ? submission!.Link : null,
                            mark,
                            WaitingForMarks: mark == null && !a.MarksReleased);
                    })
                    .Concat(visibleMarks.Where(m => m.ModuleId == module.ModuleId && m.AssessmentId == null)
                        .Select(m => new PortfolioEntry(m.AssessmentName, "Other", null, null, "Not Submitted", null, m, WaitingForMarks: false)))
                    .OrderByDescending(e => e.DueDate ?? e.Mark?.DateCaptured)
                    .ToList();

                var moduleMarks = visibleMarks.Where(m => m.ModuleId == module.ModuleId).ToList();
                return new PortfolioModule(module.ModuleId, module.Code, module.Name, module.Term, entries,
                    moduleMarks.Count == 0 ? null : RiskCalculator.AveragePercentage(moduleMarks));
            }).ToList();
        }
    }
}
