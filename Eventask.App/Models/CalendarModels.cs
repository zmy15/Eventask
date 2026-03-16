using System;
using System.Collections.Generic;

namespace Eventask.App.Models
{
    public class CalendarDay
    {
        public DateTime Date { get; set; }
        public string DayNum => Date.Day.ToString();
        public string LunarText { get; set; } = string.Empty;
        public string? HolidayName { get; set; }
        public bool IsHoliday => !string.IsNullOrEmpty(HolidayName);
        public string SecondaryText => string.IsNullOrEmpty(HolidayName) ? LunarText : HolidayName;
        public bool IsToday { get; set; }
        public bool IsCurrentMonth { get; set; }
    }

    public class MonthModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName => $"{Month}月";
        public List<CalendarDay> Days { get; set; } = new();
    }

    public class YearModel
    {
        public int Year { get; set; }
        public string YearHeader => $"{Year}年";
        public List<MonthModel> Months { get; set; } = new();

        public override string ToString()
        {
            return YearHeader;
        }
    }
}