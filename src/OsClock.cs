using System;
using ObenseuerCore;

namespace ObenseuerOS
{
    [Obsolete("Use WorldClock.Now in the framework")]
    public struct OsTime
    {
        public readonly int Year;
        public readonly int Month;
        public readonly int CalendarDay;
        public readonly int Day;
        public readonly DayOfWeek Weekday;
        public readonly int Hour;
        public readonly int Minute;

        public OsTime(int year, int month, int calendarDay, int day, DayOfWeek weekday, int hour, int minute)
        {
            Year = year;
            Month = month;
            CalendarDay = calendarDay;
            Day = day;
            Weekday = weekday;
            Hour = hour;
            Minute = minute;
        }
    }

    [Obsolete("Use WorldClock.Now in the framework")]
    public static class OsClock
    {
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);

        public static event Action<OsTime> Changed;

        static OsClock()
        {
            Events.HourPassed += OnHour;
        }

        public static bool IsAvailable
        {
            get
            {
                return WorldClock.IsAvailable;
            }
        }

        public static OsTime Now
        {
            get
            {
                WorldTime now = WorldClock.Now;
                return new OsTime(now.Year, now.Month, now.CalendarDay, now.Day, now.Weekday, now.Hour, now.Minute);
            }
        }

        private static void OnHour(int hour)
        {
            if (Changed == null) return;
            OsTime now = Now;
            OsTime atHour = new OsTime(now.Year, now.Month, now.CalendarDay, now.Day, now.Weekday, hour, 0);
            foreach (Delegate handler in Changed.GetInvocationList())
            {
                try
                {
                    ((Action<OsTime>)handler)(atHour);
                }
                catch (Exception e)
                {
                    Log.Error("clock handler failed: " + e);
                }
            }
        }
    }
}
