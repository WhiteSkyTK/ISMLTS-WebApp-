using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Repositories;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class TermsTests : IDisposable
    {
        private static readonly Term Term1 = new() { TermId = 1, Name = "2026 Term 1", Code = "Term1", StartDate = new DateTime(2026, 2, 2), EndDate = new DateTime(2026, 6, 19) };
        private static readonly Term Term2 = new() { TermId = 2, Name = "2026 Term 2", Code = "Term2", StartDate = new DateTime(2026, 7, 13), EndDate = new DateTime(2026, 11, 20) };
        private static readonly Term NextYear = new() { TermId = 3, Name = "2027 Term 1", Code = "Term1", StartDate = new DateTime(2027, 2, 1), EndDate = new DateTime(2027, 6, 18) };
        private static readonly Term[] All = [Term1, Term2, NextYear];

        private readonly SqliteTestDb _db = new();

        [Theory]
        [InlineData("2026-01-15", null)]
        [InlineData("2026-03-01", 1)]
        [InlineData("2026-07-01", 1)] // the holiday after Term 1 still belongs to it
        [InlineData("2026-10-02", 2)]
        [InlineData("2027-02-01", 3)]
        public void Current_IsTheLatestTermThatHasStarted(string today, int? expected)
        {
            Assert.Equal(expected, Terms.Current(All, DateTime.Parse(today))?.TermId);
        }

        [Fact]
        public void WithoutTerms_EveryModuleIsCurrent()
        {
            Assert.True(Terms.IsCurrent("Term1", null));
            Assert.True(Terms.IsCurrent("Term2", Term2));
            Assert.False(Terms.IsCurrent("Term1", Term2));
        }

        [Fact]
        public void Periods_UseEachCodesLatestStartedTerm_FromItsFirstToItsLastDay()
        {
            var periods = Terms.Periods(All, new DateTime(2026, 10, 2));

            Assert.Equal(2, periods.Windows.Count);
            var (from, to) = periods.Windows["Term1"];
            Assert.Equal(DateTime.SpecifyKind(new DateTime(2026, 2, 2), DateTimeKind.Local).ToUniversalTime(), from);
            Assert.Equal(DateTime.SpecifyKind(new DateTime(2026, 6, 20), DateTimeKind.Local).ToUniversalTime(), to);
            Assert.Empty(Terms.Periods([], DateTime.Today).Windows);
        }

        [Fact]
        public void Problem_ChecksTheDatesAndTheCode()
        {
            Assert.Null(Terms.Problem(Term1));
            Assert.Equal("EndDate", Terms.Problem(new Term { Code = "Term1", StartDate = new DateTime(2026, 5, 1), EndDate = new DateTime(2026, 4, 1) })?.Field);
            Assert.Equal("Code", Terms.Problem(new Term { Code = "Term9", StartDate = DateTime.Today, EndDate = DateTime.Today })?.Field);
        }

        [Theory]
        [InlineData("2026-10-05 08:50", true)]  // Monday, 10 minutes early
        [InlineData("2026-10-05 10:29", true)]
        [InlineData("2026-10-05 08:30", false)] // too early
        [InlineData("2026-10-05 10:30", false)] // over
        [InlineData("2026-10-06 09:00", false)] // Tuesday
        public void Timetable_FindsTheClassOnNow(string now, bool found)
        {
            var slot = new TimetableSlot { Day = DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 30), Venue = "Room 1" };

            Assert.Equal(found, Timetable.Now([slot], DateTime.Parse(now)) != null);
        }

        [Fact]
        public void Timetable_ChecksEachClass()
        {
            Assert.Equal("EndTime", Timetable.Problem(new TimetableSlot { Day = DayOfWeek.Monday, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(9, 0), Venue = "Room 1" })?.Field);
            Assert.Equal("Day", Timetable.Problem(new TimetableSlot { Day = DayOfWeek.Sunday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0), Venue = "Room 1" })?.Field);
            Assert.Equal("Venue", Timetable.Problem(new TimetableSlot { Day = DayOfWeek.Friday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0), Venue = " " })?.Field);
        }

        [Fact]
        public async Task Attendance_CountsOnlyTheTermsClasses_AndNotCancelledOnes()
        {
            int moduleId, studentId;
            await using (var setup = _db.NewContext())
            {
                var module = new Module { Code = "PROG6211", Name = "Programming", Term = "Term1", Lecturer = new Lecturer { FullName = "L", Email = "l@x.test", PasswordHash = "x" } };
                var student = new Student { FullName = "S", Email = "s@rcconnect.edu.za", PasswordHash = "x", Modules = { module } };
                AttendanceSession Session(string code, DateTime local, bool cancelled = false) => new()
                {
                    Module = module, Code = code, IsCancelled = cancelled, IsClosed = true,
                    StartedAt = DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime(),
                    ExpiresAt = DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime().AddMinutes(15),
                    Records = { new AttendanceRecord { Student = student, ScannedAt = DateTime.UtcNow } }
                };
                setup.AddRange(student,
                    Session("LAST01", new DateTime(2025, 9, 1, 9, 0, 0)),          // last year
                    Session("IN0001", new DateTime(2026, 2, 2, 9, 0, 0)),          // first day
                    Session("IN0002", new DateTime(2026, 6, 19, 15, 0, 0)),        // last day
                    Session("GONE01", new DateTime(2026, 3, 2, 9, 0, 0), true),    // cancelled
                    Session("LATE01", new DateTime(2026, 6, 22, 9, 0, 0)));        // after the term
                await setup.SaveChangesAsync();
                moduleId = module.ModuleId;
                studentId = student.StudentId;
            }
            await using var context = _db.NewContext();
            var repository = new AttendanceRepository(context);
            var periods = Terms.Periods([Term1], new DateTime(2026, 10, 2));

            Assert.Equal(2, (await repository.CountSessionsByModuleAsync([moduleId], periods))[moduleId]);
            Assert.Equal(2, (await repository.GetRecordsByStudentAsync(studentId, periods)).Count());
            Assert.Equal(2, (await repository.GetRecordsByModulesAsync([moduleId], periods)).Count);
            // No terms: everything except the cancelled class
            Assert.Equal(4, (await repository.CountSessionsByModuleAsync([moduleId], AttendancePeriods.All))[moduleId]);
        }

        public void Dispose() => _db.Dispose();
    }
}
