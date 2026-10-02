using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;

namespace ISMLTS_WebApp_.Services
{
    public static class Terms
    {
        // The latest term that has started is current, also in the holidays after it, until the next one starts
        public static Term? Current(IEnumerable<Term> terms, DateTime today) =>
            terms.Where(t => t.StartDate.Date <= today.Date).OrderByDescending(t => t.StartDate).FirstOrDefault();

        // With no terms at all, every module counts as current
        public static bool IsCurrent(string moduleTerm, Term? current) => current == null || current.Code == moduleTerm;

        // For each code, the latest term with that code that has started: its sessions are the ones that count
        public static AttendancePeriods Periods(IEnumerable<Term> terms, DateTime today) =>
            new(terms.Where(t => t.StartDate.Date <= today.Date)
                .GroupBy(t => t.Code)
                .ToDictionary(g => g.Key, g =>
                {
                    var term = g.OrderByDescending(t => t.StartDate).First();
                    return (LocalMidnightUtc(term.StartDate), LocalMidnightUtc(term.EndDate.AddDays(1)));
                }));

        // The term a date falls in (used to place timetabled classes on the calendar)
        public static Term? Containing(IEnumerable<Term> terms, string code, DateTime day) =>
            terms.FirstOrDefault(t => t.Code == code && t.StartDate.Date <= day.Date && day.Date <= t.EndDate.Date);

        // What's wrong with a term the admin entered, or null
        public static (string Field, string Message)? Problem(Term term)
        {
            if (term.Code is not (CourseTerms.Term1 or CourseTerms.Term2)) return (nameof(Term.Code), "Pick Term 1 or Term 2.");
            if (term.EndDate.Date < term.StartDate.Date) return (nameof(Term.EndDate), "The last day must be on or after the first day.");
            if ((term.EndDate - term.StartDate).TotalDays > 366) return (nameof(Term.EndDate), "A term can be a year long at most.");
            return null;
        }

        public static string Label(string code) => code == CourseTerms.Term2 ? "Term 2" : "Term 1";

        private static DateTime LocalMidnightUtc(DateTime day) =>
            DateTime.SpecifyKind(day.Date, DateTimeKind.Local).ToUniversalTime();
    }

    public interface ITermService
    {
        Task<List<Term>> AllAsync();
        Task<Term?> CurrentAsync();
        Task<AttendancePeriods> AttendancePeriodsAsync();
    }

    public class TermService : ITermService
    {
        private readonly ITermRepository _terms;
        private readonly TimeProvider _time;

        public TermService(ITermRepository terms, TimeProvider time)
        {
            _terms = terms;
            _time = time;
        }

        public Task<List<Term>> AllAsync() => _terms.GetOrderedAsync();

        public async Task<Term?> CurrentAsync() => Terms.Current(await _terms.GetOrderedAsync(), Today);

        public async Task<AttendancePeriods> AttendancePeriodsAsync() => Terms.Periods(await _terms.GetOrderedAsync(), Today);

        private DateTime Today => _time.GetLocalNow().Date;
    }
}
