using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Services
{
    // Builds a module's markbook: one row per enrolled student, one column per assessment
    public static class Markbook
    {
        public static MarkbookViewModel Build(
            Module module,
            IEnumerable<Student> students,
            IEnumerable<Assessment> assessments,
            IEnumerable<Mark> marks,
            IEnumerable<Submission> submissions)
        {
            var columns = assessments.OrderBy(a => a.DueDate).ThenBy(a => a.Name).ToList();
            var enrolled = students.OrderBy(s => s.FullName).ToList();
            var enrolledIds = enrolled.Select(s => s.StudentId).ToHashSet();

            // Marks of students who have since left the module don't count
            var moduleMarks = marks.Where(m => m.ModuleId == module.ModuleId && enrolledIds.Contains(m.StudentId)).ToList();
            var marksByCell = moduleMarks.Where(m => m.AssessmentId != null)
                .GroupBy(m => (m.StudentId, AssessmentId: m.AssessmentId!.Value))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.MarkId).First());
            var submissionsByCell = submissions
                .GroupBy(s => (s.StudentId, s.AssessmentId))
                .ToDictionary(g => g.Key, g => g.First());

            var rows = enrolled.Select(student =>
            {
                var studentMarks = moduleMarks.Where(m => m.StudentId == student.StudentId).ToList();
                return new MarkbookRow
                {
                    StudentId = student.StudentId,
                    FullName = student.FullName,
                    Email = student.Email,
                    Cells = columns.Select(a => new MarkbookCell
                    {
                        AssessmentId = a.AssessmentId,
                        Mark = marksByCell.GetValueOrDefault((student.StudentId, a.AssessmentId)),
                        SubmissionStatus = Submission.StatusFor(submissionsByCell.GetValueOrDefault((student.StudentId, a.AssessmentId))?.SubmittedAt, a.DueDate)
                    }).ToList(),
                    OtherMarks = studentMarks.Where(m => m.AssessmentId == null).OrderBy(m => m.DateCaptured).ToList(),
                    AveragePercentage = studentMarks.Count == 0 ? null : RiskCalculator.AveragePercentage(studentMarks),
                    IsAtRisk = studentMarks.Count > 0 && RiskCalculator.IsAtRisk(studentMarks)
                };
            }).ToList();

            return new MarkbookViewModel
            {
                ModuleId = module.ModuleId,
                ModuleCode = module.Code,
                ModuleDisplay = $"{module.Code} - {module.Name}",
                Rows = rows,
                Columns = columns.Select((a, index) =>
                {
                    var cells = rows.Select(r => r.Cells[index]).ToList();
                    var marked = cells.Where(c => c.Mark != null).Select(c => c.Mark!).ToList();
                    return new MarkbookColumn
                    {
                        AssessmentId = a.AssessmentId,
                        Name = a.Name,
                        Type = a.Type,
                        DueDate = a.DueDate,
                        MaxScore = a.MaxScore,
                        MarksReleased = a.MarksReleased,
                        MarkedCount = marked.Count,
                        ToMarkCount = cells.Count(c => c.IsWaitingForMark),
                        AveragePercentage = marked.Count == 0 ? null : RiskCalculator.AveragePercentage(marked)
                    };
                }).ToList()
            };
        }
    }
}
