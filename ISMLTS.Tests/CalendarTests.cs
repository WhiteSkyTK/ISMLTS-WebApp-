using ISMLTS_WebApp_.Models;
using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public class CalendarTests
    {
        private static readonly Module Prog = new() { ModuleId = 1, Code = "PROG6211", Name = "Programming", Term = "Term2" };
        private static readonly TimetableSlot Monday = new() { SlotId = 7, ModuleId = 1, Day = DayOfWeek.Monday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 30), Venue = "Room 3, Block B" };
        private static readonly Term Term2 = new() { TermId = 2, Name = "2026 Term 2", Code = "Term2", StartDate = new DateTime(2026, 7, 13), EndDate = new DateTime(2026, 10, 9) };

        [Fact]
        public void Classes_AreOnTheirWeekday_OnlyInsideTheirTerm()
        {
            // Mon 5 Oct is in the term; Mon 12 Oct is after it ends
            var entries = CalendarBuilder.Build([Prog], [Monday], [], [Term2], new DateTime(2026, 10, 1), new DateTime(2026, 10, 15));

            var classes = entries.Where(e => e.Kind == CalendarKind.Class).ToList();
            var single = Assert.Single(classes);
            Assert.Equal(new DateTime(2026, 10, 5, 9, 0, 0), single.Start);
            Assert.Equal(("PROG6211 class", "Room 3, Block B", "class-7-20261005"), (single.Title, single.Location, single.Uid));
            Assert.Contains(entries, e => e.Title == "2026 Term 2 ends" && e.Start == new DateTime(2026, 10, 9));
        }

        [Fact]
        public void WithoutTerms_ClassesRunEveryWeek_AndDueDatesAreAllDay()
        {
            var due = new Assessment { AssessmentId = 4, ModuleId = 1, Name = "POE", DueDate = new DateTime(2026, 10, 7) };
            var other = new Assessment { AssessmentId = 5, ModuleId = 99, Name = "Someone else's", DueDate = new DateTime(2026, 10, 7) };

            var entries = CalendarBuilder.Build([Prog], [Monday], [due, other], [], new DateTime(2026, 10, 1), new DateTime(2026, 10, 15));

            Assert.Equal(2, entries.Count(e => e.Kind == CalendarKind.Class));
            var dueEntry = Assert.Single(entries, e => e.Kind == CalendarKind.Due);
            Assert.True(dueEntry.AllDay);
            Assert.Equal("PROG6211: POE due", dueEntry.Title);
        }

        [Fact]
        public void Ics_IsValidRfc5545()
        {
            var entries = new[]
            {
                new CalendarEntry("due-4", CalendarKind.Due, new DateTime(2026, 10, 7), null, "PROG6211: POE; part 1, final", null),
                new CalendarEntry("class-7-20261005", CalendarKind.Class, new DateTime(2026, 10, 5, 9, 0, 0), new DateTime(2026, 10, 5, 10, 30, 0), "PROG6211 class", "Room 3, Block B")
            };

            var ics = IcsCalendar.Write("ISMLTS", entries, new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc));

            Assert.StartsWith("BEGIN:VCALENDAR\r\nVERSION:2.0\r\n", ics);
            Assert.EndsWith("END:VCALENDAR\r\n", ics);
            Assert.DoesNotContain("\n", ics.Replace("\r\n", string.Empty));
            Assert.Contains("DTSTART;VALUE=DATE:20261007\r\nDTEND;VALUE=DATE:20261008\r\n", ics);
            Assert.Contains("SUMMARY:PROG6211: POE\\; part 1\\, final\r\n", ics);
            Assert.Contains("LOCATION:Room 3\\, Block B\r\n", ics);
            Assert.Contains("UID:class-7-20261005@ismlts\r\n", ics);
            var start = DateTime.SpecifyKind(new DateTime(2026, 10, 5, 9, 0, 0), DateTimeKind.Local).ToUniversalTime();
            Assert.Contains($"DTSTART:{start:yyyyMMdd'T'HHmmss'Z'}\r\n", ics);
            Assert.All(ics.Split("\r\n"), line => Assert.True(System.Text.Encoding.UTF8.GetByteCount(line) <= 75, line));
        }

        [Fact]
        public void Ics_FoldsLongLines()
        {
            var title = new string('x', 200);

            var ics = IcsCalendar.Write("ISMLTS", [new CalendarEntry("due-1", CalendarKind.Due, DateTime.Today, null, title, null)], DateTime.UtcNow);

            Assert.Contains("SUMMARY:" + title[..67] + "\r\n " + title[67..141] + "\r\n ", ics);
            Assert.Contains(title, ics.Replace("\r\n ", string.Empty));
        }
    }
}
