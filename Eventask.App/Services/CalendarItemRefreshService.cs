namespace Eventask.App.Services;

public interface ICalendarItemRefreshService
{
    event Action<DateTime>? MonthItemsChanged;
    void NotifyMonthItemsChanged(DateTime date);
}

public sealed class CalendarItemRefreshService : ICalendarItemRefreshService
{
    public event Action<DateTime>? MonthItemsChanged;

    public void NotifyMonthItemsChanged(DateTime date)
    {
        MonthItemsChanged?.Invoke(date.Date);
    }
}
