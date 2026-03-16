using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eventask.App.Models;
using Eventask.App.Services;
using Eventask.App.Services.Generated;
using Eventask.Domain.Dtos;
using Eventask.Domain.Requests;
using Refit;

namespace Eventask.App.ViewModels
{
    public enum CalendarMode { Year, Month, Day }

    public partial class MainViewModel : ObservableObject
    {
        private readonly ChineseLunisolarCalendar _lunarCalendar = new();
        private static readonly Dictionary<(int Month, int Day), string> _solarHolidays = new()
        {
            { (1, 1), "元旦" },
            { (2, 14), "情人节" },
            { (3, 8), "妇女节" },
            { (4, 5), "清明节" },
            { (5, 1), "劳动节" },
            { (6, 1), "儿童节" },
            { (10, 1), "国庆节" },
            { (12, 24), "平安夜" },
            { (12, 25), "圣诞节" }
        };

        private static readonly Dictionary<(int Month, int Day), string> _lunarHolidays = new()
        {
            { (1, 1), "春节" },
            { (1, 15), "元宵节" },
            { (5, 5), "端午节" },
            { (7, 7), "七夕" },
            { (8, 15), "中秋节" },
            { (9, 9), "重阳节" }
        };
        private readonly string[] _chineseNumbers = { "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
        private readonly INavigationService? _navigationService;
        private readonly IAuthService? _authService;
        private readonly ICalendarStateService? _calendarStateService;
        private readonly IEventaskApi? _api;

        // 新增标志位,防止滚动更新日期时触发数据加载
        private bool _isScrolling = false;

        // 当前选中的实际日期(用于创建日程等操作)
        [ObservableProperty]
        private DateTime _selectedDate = DateTime.Now;

        // 当前显示的日期(用于年视图标题显示)
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(YearHeaderText))]
        [NotifyPropertyChangedFor(nameof(MonthHeaderText))]
        [NotifyPropertyChangedFor(nameof(FullDateHeaderText))]
        private DateTime _currentDate = DateTime.Now;

        [ObservableProperty]
        private CalendarMode _currentMode = CalendarMode.Month;

        public ObservableCollection<YearModel> YearGroups { get; } = new();

        [ObservableProperty]
        private IEnumerable<CalendarDay> _monthViewDays = new List<CalendarDay>();

        [ObservableProperty]
        private bool _isSearchVisible = false;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isSearching = false;

        [ObservableProperty]
        private bool _hasSearchResults = false;

        public ObservableCollection<SearchResultItem> SearchResults { get; } = new();

        public string YearHeaderText => $"{CurrentDate.Year}年";
        public string MonthHeaderText => $"{CurrentDate.Month}月";
        public string FullDateHeaderText => $"{CurrentDate:yyyy年}";

        public MainViewModel()
        {
            GenerateYearGroup(CurrentDate.Year);
            GenerateYearGroup(CurrentDate.Year - 1);
            GenerateYearGroup(CurrentDate.Year + 1);

            GenerateMonthViewData();
        }

        public MainViewModel(
            INavigationService navigationService,
            IAuthService authService,
            ICalendarStateService calendarStateService,
            IEventaskApi api) : this()
        {
            _navigationService = navigationService;
            _authService = authService;
            _calendarStateService = calendarStateService;
            _api = api;

            // 异步初始化日历状态
            _ = InitializeCalendarStateAsync();
        }

        /// <summary>
        /// 初始化日历状态,确保有可用的日历ID
        /// </summary>
        private async Task InitializeCalendarStateAsync()
        {
            if (_calendarStateService != null)
            {
                try
                {
                    await _calendarStateService.EnsureCalendarSelectedAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"初始化日历状态失败: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            if (CurrentMode == CalendarMode.Day)
            {
                CurrentMode = CalendarMode.Month;
            }
            else if (CurrentMode == CalendarMode.Month)
            {
                EnsureYearLoaded(CurrentDate.Year);
                CurrentMode = CalendarMode.Year;
            }
        }

        [RelayCommand]
        private void SelectMonth(MonthModel monthModel)
        {
            if (monthModel == null)
                return;
            CurrentMode = CalendarMode.Month;
            SelectedDate = new DateTime(monthModel.Year, monthModel.Month, 1);
            CurrentDate = SelectedDate;
        }

        [RelayCommand]
        private void SelectDate(CalendarDay day)
        {
            if (day == null)
                return;
            SelectedDate = day.Date;
            CurrentDate = day.Date;
            _navigationService?.NavigateToDayDetail(day.Date);
        }

        [RelayCommand]
        private async Task AddTaskAsync()
        {
            // 确保已加载日历ID
            var calendarId = Guid.Empty;
            if (_calendarStateService != null)
            {
                calendarId = await _calendarStateService.EnsureCalendarSelectedAsync();
            }

            if (calendarId == Guid.Empty)
            {
                // TODO: 显示错误消息 - 没有可用的日历
                System.Diagnostics.Debug.WriteLine("没有可用的日历,无法创建任务");
                return;
            }

            // 使用 SelectedDate 而不是 CurrentDate
            _navigationService?.NavigateToEditScheduleItem(
                calendarId: calendarId,
                itemType: ScheduleItemType.Task,
                defaultDate: SelectedDate);
        }

        [RelayCommand]
        private async Task RecognizeWithAiAsync()
        {
            if (_navigationService == null || _calendarStateService == null || _api == null)
            {
                return;
            }

            var calendarId = await _calendarStateService.EnsureCalendarSelectedAsync();
            if (calendarId == Guid.Empty)
            {
                return;
            }

            var drafts = await _navigationService.OpenNaturalLanguageDialogAsync(SelectedDate);
            if (drafts == null || drafts.Count == 0)
            {
                return;
            }

            foreach (var draft in drafts)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(draft.Title))
                    {
                        continue;
                    }

                    if (draft.ItemType == ScheduleItemType.Event)
                    {
                        if (!draft.StartDate.HasValue || !draft.EndDate.HasValue || !draft.StartTime.HasValue || !draft.EndTime.HasValue)
                        {
                            continue;
                        }

                        var request = new CreateScheduleItemRequest(
                            Type: "Event",
                            Title: draft.Title.Trim(),
                            Description: draft.Description,
                            Location: draft.Location,
                            StartAt: ToUtcDateTimeOffset(draft.StartDate.Value, draft.StartTime.Value),
                            EndAt: ToUtcDateTimeOffset(draft.EndDate.Value, draft.EndTime.Value),
                            DueAt: null,
                            AllDay: draft.AllDay
                        );

                        await _api.ItemsPostAsync(calendarId, request);
                    }
                    else
                    {
                        var dueDate = draft.DueDate ?? SelectedDate.Date;
                        var dueTime = draft.DueTime ?? new TimeSpan(18, 0, 0);

                        var request = new CreateScheduleItemRequest(
                            Type: "Task",
                            Title: draft.Title.Trim(),
                            Description: draft.Description,
                            Location: draft.Location,
                            StartAt: null,
                            EndAt: null,
                            DueAt: ToUtcDateTimeOffset(dueDate, dueTime),
                            AllDay: draft.AllDay
                        );

                        await _api.ItemsPostAsync(calendarId, request);
                    }
                }
                catch (ApiException ex)
                {
                    Debug.WriteLine($"AI 草稿创建失败: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"处理 AI 草稿时发生错误: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private void CreateCalendar()
        {
            _navigationService?.NavigateToCreateCalendar();
        }

        [RelayCommand]
        private void SelectCalendar()
        {
            _navigationService?.NavigateToSelectCalendar();
        }

        [RelayCommand]
        private async Task ManageCalendarMembersAsync()
        {
            if (_calendarStateService == null || _navigationService == null)
                return;

            var calendarId = await _calendarStateService.EnsureCalendarSelectedAsync();
            if (calendarId == Guid.Empty)
            {
                System.Diagnostics.Debug.WriteLine("没有选中的日历,无法管理成员");
                return;
            }

            var calendarName = _calendarStateService.CurrentCalendarName ?? "日历";
            _navigationService.NavigateToCalendarMembers(calendarId, calendarName);
        }

        [RelayCommand]
        private void ToggleSearch()
        {
            IsSearchVisible = !IsSearchVisible;
            if (!IsSearchVisible)
            {
                SearchText = string.Empty;
                SearchResults.Clear();
                HasSearchResults = false;
            }
        }

        [RelayCommand]
        private void OpenSettings()
        {
            _navigationService?.NavigateToSettings();
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                SearchResults.Clear();
                HasSearchResults = false;
                return;
            }

            if (_api == null || _calendarStateService == null)
            {
                return;
            }

            try
            {
                IsSearching = true;
                SearchResults.Clear();

                var calendarId = await _calendarStateService.EnsureCalendarSelectedAsync();
                if (calendarId == Guid.Empty)
                {
                    Debug.WriteLine("没有选中的日历,无法搜索");
                    return;
                }

                // 搜索前后一年范围内的日程和任务
                var fromDate = new DateTimeOffset(DateTime.SpecifyKind(DateTime.Today.AddYears(-1), DateTimeKind.Unspecified), TimeSpan.Zero);
                var toDate = new DateTimeOffset(DateTime.SpecifyKind(DateTime.Today.AddYears(1), DateTimeKind.Unspecified), TimeSpan.Zero);

                var items = await _api.ItemsGetAsync(calendarId, from: fromDate, to: toDate);

                var searchTerm = SearchText.Trim().ToLowerInvariant();

                var matchedItems = items
                    .Where(item => !item.IsDeleted)
                    .Where(item =>
                        (item.Title?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (item.Description?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (item.Location?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false))
                    .OrderByDescending(item => item.StartAt ?? item.DueAt ?? item.UpdatedAt)
                    .Take(50); // 限制结果数量

                foreach (var item in matchedItems)
                {
                    var resultItem = new SearchResultItem
                    {
                        Id = item.Id,
                        CalendarId = item.CalendarId,
                        Title = item.Title,
                        Description = item.Description,
                        Location = item.Location,
                        IsTask = item.Type == "Task",
                        IsCompleted = item.IsCompleted,
                        Date = item.Type == "Task"
                            ? item.DueAt?.LocalDateTime.Date
                            : item.StartAt?.LocalDateTime.Date,
                        TimeText = GetTimeDisplayText(item)
                    };
                    SearchResults.Add(resultItem);
                }

                HasSearchResults = SearchResults.Count > 0;
            }
            catch (ApiException ex)
            {
                Debug.WriteLine($"搜索失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"搜索时发生错误: {ex.Message}");
            }
            finally
            {
                IsSearching = false;
            }
        }

        [RelayCommand]
        private void SelectSearchResult(SearchResultItem result)
        {
            if (result?.Date == null)
                return;

            // 导航到日期详情页
            _navigationService?.NavigateToDayDetail(result.Date.Value);
        }

        private static string GetTimeDisplayText(ScheduleItemDto item)
        {
            if (item.Type == "Task")
            {
                if (item.DueAt.HasValue)
                {
                    return $"{item.DueAt.Value.LocalDateTime:yyyy-MM-dd HH:mm} 截止";
                }
                return "无截止时间";
            }
            else
            {
                if (item.AllDay)
                {
                    return item.StartAt.HasValue
                        ? $"{item.StartAt.Value.LocalDateTime:yyyy-MM-dd} 全天"
                        : "全天";
                }
                if (item.StartAt.HasValue && item.EndAt.HasValue)
                {
                    return $"{item.StartAt.Value.LocalDateTime:yyyy-MM-dd HH:mm} - {item.EndAt.Value.LocalDateTime:HH:mm}";
                }
                return string.Empty;
            }
        }

        [RelayCommand]
        private async Task LogoutAsync()
        {
            try
            {
                // 清除本地 token
                if (_authService != null)
                {
                    await _authService.ClearTokenAsync();
                }

                // 清除当前日历选择
                if (_calendarStateService != null)
                {
                    await _calendarStateService.ClearCurrentCalendarAsync();
                }

                // 导航到登录页
                _navigationService?.NavigateToLogin();
            }
            catch (Exception ex)
            {
                // 记录错误但仍然导航到登录页
                System.Diagnostics.Debug.WriteLine($"退出登录时发生错误: {ex.Message}");
                _navigationService?.NavigateToLogin();
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            // 当搜索文本清空时，清除搜索结果
            if (string.IsNullOrWhiteSpace(value))
            {
                SearchResults.Clear();
                HasSearchResults = false;
            }
        }

        public void ChangeMonth(int offset)
        {
            SelectedDate = SelectedDate.AddMonths(offset);
            CurrentDate = SelectedDate;
        }

        public bool LoadPreviousYear()
        {
            if (YearGroups.Count == 0)
                return false;
            int minYear = YearGroups.Min(y => y.Year);
            return GenerateYearGroup(minYear - 1);
        }

        public bool LoadNextYear()
        {
            if (YearGroups.Count == 0)
                return false;
            int maxYear = YearGroups.Max(y => y.Year);
            return GenerateYearGroup(maxYear + 1);
        }

        private void EnsureYearLoaded(int year)
        {
            GenerateYearGroup(year - 1);
            GenerateYearGroup(year);
            GenerateYearGroup(year + 1);
        }

        // 供 View 层在滚动时调用,只更新显示日期不触发数据加载
        public void UpdateCurrentDateFromScroll(DateTime date)
        {
            if (CurrentDate.Year == date.Year && CurrentDate.Month == date.Month && CurrentDate.Day == date.Day)
                return;

            _isScrolling = true;
            try
            {
                CurrentDate = date;
                // 不更新 SelectedDate,保持用户最后选择的日期
            }
            finally
            {
                _isScrolling = false;
            }
        }

        partial void OnCurrentDateChanged(DateTime value)
        {
            if (CurrentMode == CalendarMode.Month)
            {
                GenerateMonthViewData();
            }
            else if (CurrentMode == CalendarMode.Year)
            {
                if (!_isScrolling)
                {
                    EnsureYearLoaded(value.Year);
                }
            }
        }

        private bool GenerateYearGroup(int year)
        {
            if (YearGroups.Any(y => y.Year == year))
                return false;

            var yearModel = new YearModel { Year = year };
            for (int m = 1; m <= 12; m++)
            {
                yearModel.Months.Add(new MonthModel
                {
                    Year = year,
                    Month = m,
                    Days = GetDaysForMonth(year, m, false)
                });
            }

            var index = YearGroups.Count(y => y.Year < year);
            YearGroups.Insert(index, yearModel);

            return true;
        }

        private void GenerateMonthViewData()
        {
            MonthViewDays = GetDaysForMonth(CurrentDate.Year, CurrentDate.Month, true);
        }

        private List<CalendarDay> GetDaysForMonth(int year, int month, bool detailedLunar)
        {
            var list = new List<CalendarDay>();
            var firstDay = new DateTime(year, month, 1);
            int offset = (int)firstDay.DayOfWeek;
            var startDisplayDate = firstDay.AddDays(-offset);

            for (int i = 0; i < 42; i++)
            {
                var date = startDisplayDate.AddDays(i);
                list.Add(new CalendarDay
                {
                    Date = date,
                    IsToday = date.Date == DateTime.Today,
                    IsCurrentMonth = date.Month == month,
                    LunarText = detailedLunar ? GetLunarString(date) : string.Empty,
                    HolidayName = GetHolidayName(date)
                });
            }
            return list;
        }

        private string? GetHolidayName(DateTime date)
        {
            if (_solarHolidays.TryGetValue((date.Month, date.Day), out var solarHoliday))
            {
                return solarHoliday;
            }

            if (date.Month == 5 && IsNthWeekdayOfMonth(date, DayOfWeek.Sunday, 2))
            {
                return "母亲节";
            }

            if (date.Month == 6 && IsNthWeekdayOfMonth(date, DayOfWeek.Sunday, 3))
            {
                return "父亲节";
            }

            try
            {
                int lunarYear = _lunarCalendar.GetYear(date);
                int rawLunarMonth = _lunarCalendar.GetMonth(date);
                int lunarDay = _lunarCalendar.GetDayOfMonth(date);
                int leapMonth = _lunarCalendar.GetLeapMonth(lunarYear);

                if (leapMonth > 0)
                {
                    if (rawLunarMonth > leapMonth)
                    {
                        rawLunarMonth--;
                    }
                    else if (rawLunarMonth == leapMonth)
                    {
                        rawLunarMonth--;
                    }
                }

                if (_lunarHolidays.TryGetValue((rawLunarMonth, lunarDay), out var lunarHoliday))
                {
                    return lunarHoliday;
                }
            }
            catch
            {
                // 忽略农历计算异常
            }

            return null;
        }

        private static bool IsNthWeekdayOfMonth(DateTime date, DayOfWeek targetDayOfWeek, int occurrence)
        {
            if (occurrence < 1 || date.DayOfWeek != targetDayOfWeek)
            {
                return false;
            }

            var weekIndex = ((date.Day - 1) / 7) + 1;
            return weekIndex == occurrence;
        }

        private string GetLunarString(DateTime date)
        {
            try
            {
                int lMonth = _lunarCalendar.GetMonth(date);
                int lDay = _lunarCalendar.GetDayOfMonth(date);

                if (lDay == 1)
                    return $"{lMonth}月";

                string[] prefix = { "初", "十", "廿", "三" };
                int d = lDay;
                if (d <= 10)
                    return "初" + _chineseNumbers[d % 10 == 0 ? 9 : d % 10 - 1];
                if (d < 20)
                    return "十" + (d == 10 ? "" : _chineseNumbers[d % 10 - 1]);
                if (d == 20)
                    return "二十";
                if (d < 30)
                    return "廿" + _chineseNumbers[d % 10 - 1];
                return "三十";
            }
            catch
            {
                return "";
            }
        }

        private static DateTimeOffset ToUtcDateTimeOffset(DateTimeOffset date, TimeSpan time)
        {
            var dateTime = date.Date.Add(time);
            var unspecifiedDateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
            return new DateTimeOffset(unspecifiedDateTime, TimeSpan.Zero);
        }
    }

    /// <summary>
    /// 搜索结果项
    /// </summary>
    public partial class SearchResultItem : ObservableObject
    {
        [ObservableProperty]
        private Guid _id;

        [ObservableProperty]
        private Guid _calendarId;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string? _description;

        [ObservableProperty]
        private string? _location;

        [ObservableProperty]
        private bool _isTask;

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private DateTime? _date;

        [ObservableProperty]
        private string _timeText = string.Empty;

        public string TypeText => IsTask ? "任务" : "日程";
    }
}