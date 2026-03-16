using System;
using System.Collections.Generic;

namespace Eventask.App.Models;

public sealed class HolidayOptions
{
    public List<FixedHolidayOption> SolarHolidays { get; set; } = new();
    public List<FixedHolidayOption> LunarHolidays { get; set; } = new();
    public List<WeekdayHolidayOption> WeekdayHolidays { get; set; } = new();
}

public sealed class FixedHolidayOption
{
    public int Month { get; set; }
    public int Day { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class WeekdayHolidayOption
{
    public int Month { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public int Occurrence { get; set; }
    public string Name { get; set; } = string.Empty;
}
