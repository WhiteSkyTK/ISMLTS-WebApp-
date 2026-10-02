using System.Globalization;
using ISMLTS_WebApp_.Models;

namespace ISMLTS_WebApp_.Services
{
    public static class Timetable
    {
        public static readonly IReadOnlyList<DayOfWeek> TeachingDays =
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday];

        // "Take register" shows from this long before a class starts until it ends
        public static readonly TimeSpan Lead = TimeSpan.FromMinutes(15);

        // The class happening now (or about to start), earliest first
        public static TimetableSlot? Now(IEnumerable<TimetableSlot> slots, DateTime localNow)
        {
            var time = localNow.TimeOfDay;
            return slots
                .Where(s => s.Day == localNow.DayOfWeek && time >= s.StartTime.ToTimeSpan() - Lead && time < s.EndTime.ToTimeSpan())
                .OrderBy(s => s.StartTime)
                .FirstOrDefault();
        }

        public static (string Field, string Message)? Problem(TimetableSlot slot)
        {
            if (!TeachingDays.Contains(slot.Day)) return (nameof(TimetableSlot.Day), "Pick a day from Monday to Saturday.");
            if (slot.EndTime <= slot.StartTime) return (nameof(TimetableSlot.EndTime), "The class must end after it starts.");
            if (string.IsNullOrWhiteSpace(slot.Venue)) return (nameof(TimetableSlot.Venue), "Say where the class is.");
            return null;
        }

        public static string Times(TimetableSlot slot) =>
            $"{slot.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture)}–{slot.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture)}";

        public static string Day(DayOfWeek day) => CultureInfo.InvariantCulture.DateTimeFormat.GetDayName(day);
    }
}
