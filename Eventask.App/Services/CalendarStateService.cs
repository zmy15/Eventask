using Eventask.App.Services.Generated;

namespace Eventask.App.Services;

/// <summary>
/// Default implementation of ICalendarStateService that manages the currently selected calendar
/// and coordinates with ILocalStorageService for persistence.
/// </summary>
public class CalendarStateService : ICalendarStateService
{
    private const string CalendarIdKey = "CurrentCalendarId";
    private const string CalendarNameKey = "CurrentCalendarName";

    private readonly ILocalStorageService _localStorage;
    private readonly IEventaskApi _api;

    private Guid _currentCalendarId = Guid.Empty;
    private string? _currentCalendarName;

    public CalendarStateService(ILocalStorageService localStorage, IEventaskApi api)
    {
        _localStorage = localStorage;
        _api = api;
    }

    public Guid CurrentCalendarId => _currentCalendarId;

    public string? CurrentCalendarName => _currentCalendarName;

    public async Task SetCurrentCalendarAsync(Guid calendarId, string? calendarName = null)
    {
        _currentCalendarId = calendarId;
        _currentCalendarName = calendarName;
        await _localStorage.SetAsync(CalendarIdKey, calendarId.ToString());

        if (!string.IsNullOrEmpty(calendarName))
        {
            await _localStorage.SetAsync(CalendarNameKey, calendarName);
        }
        else
        {
            await _localStorage.RemoveAsync(CalendarNameKey);
        }
    }

    public async Task ClearCurrentCalendarAsync()
    {
        _currentCalendarId = Guid.Empty;
        _currentCalendarName = null;
        await _localStorage.RemoveAsync(CalendarIdKey);
        await _localStorage.RemoveAsync(CalendarNameKey);
    }

    public async Task<bool> TryLoadCurrentCalendarAsync()
    {
        var storedId = await _localStorage.GetAsync(CalendarIdKey);

        if (string.IsNullOrEmpty(storedId) || !Guid.TryParse(storedId, out var calendarId))
        {
            return false;
        }

        _currentCalendarId = calendarId;
        _currentCalendarName = await _localStorage.GetAsync(CalendarNameKey);
        return true;
    }

    /// <summary>
    /// Ensures a valid calendar is selected by loading from storage or fetching the first available calendar.
    /// </summary>
    public async Task<Guid> EnsureCalendarSelectedAsync()
    {
        // 如果已有有效的 CalendarId 和名称,直接返回
        if (_currentCalendarId != Guid.Empty && !string.IsNullOrEmpty(_currentCalendarName))
        {
            return _currentCalendarId;
        }

        // 如果有 ID 但没有名称,尝试从存储加载名称
        if (_currentCalendarId != Guid.Empty && string.IsNullOrEmpty(_currentCalendarName))
        {
            _currentCalendarName = await _localStorage.GetAsync(CalendarNameKey);

            // 如果存储中也没有名称,从 API 获取
            if (string.IsNullOrEmpty(_currentCalendarName))
            {
                await TryFetchCalendarNameFromApiAsync(_currentCalendarId);
            }

            if (!string.IsNullOrEmpty(_currentCalendarName))
            {
                return _currentCalendarId;
            }
        }

        // 尝试从存储加载
        if (await TryLoadCurrentCalendarAsync() && _currentCalendarId != Guid.Empty)
        {
            // 如果加载后仍然没有名称,从 API 获取
            if (string.IsNullOrEmpty(_currentCalendarName))
            {
                await TryFetchCalendarNameFromApiAsync(_currentCalendarId);
            }
            return _currentCalendarId;
        }

        // 从 API 获取第一个可用日历
        try
        {
            var calendars = await _api.CalendarsGetAsync();
            var firstCalendar = calendars.FirstOrDefault(c => !c.IsDeleted);

            if (firstCalendar != null)
            {
                await SetCurrentCalendarAsync(firstCalendar.Id, firstCalendar.Name);
                return _currentCalendarId;
            }
        }
        catch
        {
            // 静默失败
        }

        return Guid.Empty;
    }

    /// <summary>
    /// 尝试从 API 获取日历名称
    /// </summary>
    private async Task TryFetchCalendarNameFromApiAsync(Guid calendarId)
    {
        try
        {
            var calendars = await _api.CalendarsGetAsync();
            var calendar = calendars.FirstOrDefault(c => c.Id == calendarId && !c.IsDeleted);

            if (calendar != null)
            {
                _currentCalendarName = calendar.Name;
                await _localStorage.SetAsync(CalendarNameKey, calendar.Name);
            }
        }
        catch
        {
            // 静默失败
        }
    }
}