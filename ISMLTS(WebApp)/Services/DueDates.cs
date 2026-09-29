namespace ISMLTS_WebApp_.Services
{
    public static class DueDates
    {
        // "Due today", "Due tomorrow", "Due in 5 days", "1 day overdue"
        public static string Describe(DateTime dueDate, DateTime today)
        {
            var days = (dueDate.Date - today.Date).Days;
            return days switch
            {
                0 => "Due today",
                1 => "Due tomorrow",
                > 1 => $"Due in {days} days",
                -1 => "1 day overdue",
                _ => $"{-days} days overdue"
            };
        }

        public static bool IsOverdue(DateTime dueDate, DateTime today) => dueDate.Date < today.Date;
    }
}
