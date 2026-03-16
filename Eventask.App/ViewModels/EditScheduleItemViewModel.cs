using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eventask.App.Models;
using Eventask.App.Services;
using Eventask.App.Services.Generated;
using Eventask.App.Views;
using Eventask.Domain.Requests;
using Refit;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace Eventask.App.ViewModels
{
    public enum ScheduleItemType { Event, Task }
    public enum EditMode { Create, Edit }

    public partial class EditScheduleItemViewModel : ObservableObject
    {
        private readonly IEventaskApi _api;
        private readonly INavigationService _navigationService;
        private readonly IAttachmentService _attachmentService;
        private readonly IEventImportService _eventImportService;
        private readonly ICalendarStateService _calendarStateService;

        [ObservableProperty]
        private EditMode _mode = EditMode.Create;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEventType))]
        [NotifyPropertyChangedFor(nameof(IsTaskType))]
        [NotifyPropertyChangedFor(nameof(IsCreateMode))]
        [NotifyPropertyChangedFor(nameof(PageTitle))]
        private ScheduleItemType _itemType = ScheduleItemType.Event;

        [ObservableProperty]
        private Guid? _itemId;

        [ObservableProperty]
        private Guid _calendarId;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string? _description;

        [ObservableProperty]
        private string? _location;

        [ObservableProperty]
        private DateTime _startDate = DateTime.Today;

        [ObservableProperty]
        private TimeSpan _startTime = new(9, 0, 0);

        [ObservableProperty]
        private DateTime _endDate = DateTime.Today;

        [ObservableProperty]
        private TimeSpan _endTime = new(10, 0, 0);

        [ObservableProperty]
        private DateTime? _dueDate;

        [ObservableProperty]
        private TimeSpan? _dueTime;

        [ObservableProperty]
        private bool _allDay;

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _isUploadingAttachment;

        public ObservableCollection<AttachmentModel> Attachments { get; } = new();
        public ObservableCollection<ReminderViewModel> Reminders { get; } = new();

        // 待上传的附件（创建模式下使用）
        private readonly List<string> _pendingAttachmentPaths = new();

        // 保存全天事件切换前的时间，用于恢复
        private TimeSpan? _previousStartTime;
        private TimeSpan? _previousEndTime;

        // 保存来源日期，用于编辑模式返回导航
        private DateTime? _sourceDate;

        public string PageTitle => Mode == EditMode.Create
            ? $"新建{(ItemType == ScheduleItemType.Event ? "日程" : "任务")}"
            : $"编辑{(ItemType == ScheduleItemType.Event ? "日程" : "任务")}";

        public bool IsEventType => ItemType == ScheduleItemType.Event;
        public bool IsTaskType => ItemType == ScheduleItemType.Task;
        public bool IsCreateMode => Mode == EditMode.Create;

        public EditScheduleItemViewModel(
            IEventaskApi api,
            INavigationService navigationService,
            IAttachmentService attachmentService,
            IEventImportService eventImportService,
            ICalendarStateService calendarStateService)
        {
            _api = api;
            _navigationService = navigationService;
            _attachmentService = attachmentService;
            _eventImportService = eventImportService;
            _calendarStateService = calendarStateService;
        }

        public void InitializeForCreate(Guid calendarId, ScheduleItemType itemType, DateTime? defaultDate = null)
        {
            Mode = EditMode.Create;
            ItemType = itemType;
            CalendarId = calendarId;
            ItemId = null;

            var date = defaultDate ?? DateTime.Today;
            _sourceDate = null; // 创建模式不保存来源日期，返回时导航到主页
            StartDate = date;
            EndDate = date;

            if (itemType == ScheduleItemType.Task)
            {
                DueDate = date;
                DueTime = new TimeSpan(23, 59, 0);
            }
            else
            {
                DueDate = null;
                DueTime = null;
            }

            Title = string.Empty;
            Description = null;
            Location = null;
            AllDay = false;
            IsCompleted = false;
            Attachments.Clear();
            Reminders.Clear();
            _pendingAttachmentPaths.Clear();
            _previousStartTime = null;
            _previousEndTime = null;

            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(IsEventType));
            OnPropertyChanged(nameof(IsTaskType));
            OnPropertyChanged(nameof(IsCreateMode));
        }

        public void InitializeForEdit(ScheduleItemViewModel item, Guid calendarId, DateTime sourceDate)
        {
            Mode = EditMode.Edit;
            ItemType = item.IsTask ? ScheduleItemType.Task : ScheduleItemType.Event;
            CalendarId = calendarId;
            ItemId = item.Id;
            _sourceDate = sourceDate; // 编辑模式保存来源日期，返回时导航到日期详情页

            Title = item.Title;
            Description = item.Description;
            Location = item.Location;
            IsCompleted = item.IsCompleted;
            Attachments.Clear();
            Reminders.Clear();
            _pendingAttachmentPaths.Clear();
            _previousStartTime = null;
            _previousEndTime = null;

            // 使用来源日期作为基准日期
            if (item.StartTime.HasValue && item.EndTime.HasValue)
            {
                StartDate = sourceDate;
                EndDate = sourceDate;
                StartTime = item.StartTime.Value;
                EndTime = item.EndTime.Value;
                AllDay = false;
            }

            if (item.DueTime.HasValue)
            {
                DueDate = sourceDate;
                DueTime = item.DueTime;
            }

            _ = LoadAttachmentsAsync();

            // 只有日程才加载提醒
            if (IsEventType)
            {
                _ = LoadRemindersAsync();
            }

            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(IsEventType));
            OnPropertyChanged(nameof(IsTaskType));
            OnPropertyChanged(nameof(IsCreateMode));
        }

        /// <summary>
        /// 当全天事件标志变化时自动调整时间
        /// </summary>
        partial void OnAllDayChanged(bool value)
        {
            // 只在事件类型时处理
            if (ItemType != ScheduleItemType.Event)
            {
                return;
            }

            if (value)
            {
                // 切换到全天：保存当前时间并设置为 00:00 到 23:59
                _previousStartTime = StartTime;
                _previousEndTime = EndTime;

                StartTime = TimeSpan.Zero; // 00:00:00
                EndTime = new TimeSpan(23, 59, 0); // 23:59:00
            }
            else
            {
                // 取消全天：恢复之前的时间，如果没有保存的时间则使用默认值
                StartTime = _previousStartTime ?? new TimeSpan(9, 0, 0);
                EndTime = _previousEndTime ?? new TimeSpan(10, 0, 0);
            }
        }

        private async Task LoadAttachmentsAsync()
        {
            if (!ItemId.HasValue) return;

            try
            {
                var attachments = await _attachmentService.GetAttachmentsAsync(ItemId.Value);
                Attachments.Clear();
                foreach (var attachment in attachments)
                {
                    Attachments.Add(attachment);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载附件失败: {ex.Message}");
            }
        }

        private async Task LoadRemindersAsync()
        {
            if (!ItemId.HasValue) return;

            try
            {
                // TODO: 调用 API 加载提醒
                // var reminders = await _api.GetRemindersAsync(CalendarId, ItemId.Value);
                // 暂时先清空集合
                Reminders.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载提醒失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task AddReminderAsync()
        {
            // 只有日程才能添加提醒
            if (ItemType != ScheduleItemType.Event)
            {
                return;
            }

            // 显示选择提醒时间的对话框
            var dialog = new SelectReminderDialog();

            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel == null) return;

            var result = await dialog.ShowDialog<int?>(topLevel);

            if (result.HasValue)
            {
                var reminder = new ReminderViewModel
                {
                    Id = Guid.NewGuid(),
                    OffsetMinutes = result.Value
                };
                Reminders.Add(reminder);
            }
        }

        [RelayCommand]
        private void RemoveReminder(ReminderViewModel reminder)
        {
            if (reminder == null) return;
            Reminders.Remove(reminder);
        }

        [RelayCommand]
        private async Task AddAttachmentAsync()
        {
            try
            {
                // 打开文件选择对话框
                var filePath = await SelectFileAsync();
                if (string.IsNullOrEmpty(filePath))
                {
                    return; // 用户取消选择
                }

                if (Mode == EditMode.Create)
                {
                    // 创建模式：添加到待上传列表
                    _pendingAttachmentPaths.Add(filePath);

                    // 创建临时的附件模型用于显示
                    var fileInfo = new FileInfo(filePath);
                    var tempAttachment = new AttachmentModel
                    {
                        Id = Guid.Empty, // 临时 ID
                        FileName = fileInfo.Name,
                        Size = fileInfo.Length,
                        LocalFilePath = filePath
                    };
                    Attachments.Add(tempAttachment);
                }
                else
                {
                    // 编辑模式：立即上传
                    if (!ItemId.HasValue) return;

                    IsUploadingAttachment = true;
                    ErrorMessage = null;

                    var attachment = await _attachmentService.UploadAttachmentAsync(ItemId.Value, filePath);
                    Attachments.Add(attachment);
                }
            }
            catch (ApiException ex)
            {
                ErrorMessage = $"上传附件失败: {ex.Message}";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"添加附件时发生错误: {ex.Message}";
            }
            finally
            {
                IsUploadingAttachment = false;
            }
        }

        [RelayCommand]
        private async Task ImportFromJsonAsync()
        {
            ErrorMessage = null;

            var hostWindow = GetHostWindow();
            if (hostWindow == null)
            {
                ErrorMessage = "无法打开导入对话框,请重试。";
                return;
            }

            var dialog = new ImportEventsDialog();
            var jsonContent = await dialog.ShowDialog<string?>(hostWindow);

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                return; // 用户取消
            }

            var calendarId = CalendarId;
            if (calendarId == Guid.Empty)
            {
                calendarId = await _calendarStateService.EnsureCalendarSelectedAsync();
            }

            if (calendarId == Guid.Empty)
            {
                ErrorMessage = "请先选择或创建一个日历。";
                return;
            }

            try
            {
                IsLoading = true;
                var importResult = await _eventImportService.ImportAsync(calendarId, jsonContent);
                ErrorMessage = BuildImportSummary(importResult);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"导入失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task OpenAttachmentAsync(AttachmentModel attachment)
        {
            if (attachment == null) return;

            try
            {
                // 如果是创建模式下的临时附件，直接打开本地文件
                if (Mode == EditMode.Create && attachment.Id == Guid.Empty)
                {
                    if (!string.IsNullOrEmpty(attachment.LocalFilePath) && File.Exists(attachment.LocalFilePath))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = attachment.LocalFilePath,
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    }
                }
                else
                {
                    await _attachmentService.OpenAttachmentAsync(attachment);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"打开附件失败: {ex.Message}";
            }
        }

        [RelayCommand]
        private void RemoveAttachment(AttachmentModel attachment)
        {
            if (attachment == null) return;

            // 如果是创建模式下的临时附件，从待上传列表中移除
            if (Mode == EditMode.Create && attachment.Id == Guid.Empty)
            {
                _pendingAttachmentPaths.Remove(attachment.LocalFilePath!);
            }

            Attachments.Remove(attachment);
            // TODO: 如果是已上传的附件，调用 API 删除服务器上的附件
        }

        private async Task<string?> SelectFileAsync()
        {
            try
            {
                var topLevel = Avalonia.Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                if (topLevel == null) return null;

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(
                    new Avalonia.Platform.Storage.FilePickerOpenOptions
                    {
                        Title = "选择附件",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new Avalonia.Platform.Storage.FilePickerFileType("所有文件") { Patterns = new[] { "*.*" } },
                            new Avalonia.Platform.Storage.FilePickerFileType("文档")
                            {
                                Patterns = new[] { "*.pdf", "*.doc", "*.docx", "*.txt", "*.xls", "*.xlsx" }
                            },
                            new Avalonia.Platform.Storage.FilePickerFileType("图片")
                            {
                                Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.gif", "*.bmp" }
                            },
                        }
                    });

                return files.FirstOrDefault()?.Path.LocalPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"选择文件失败: {ex.Message}");
                return null;
            }
        }

        [RelayCommand]
        private void SwitchToEvent()
        {
            if (Mode == EditMode.Edit) return;
            ItemType = ScheduleItemType.Event;
            DueDate = null;
            DueTime = null;
        }

        [RelayCommand]
        private void SwitchToTask()
        {
            if (Mode == EditMode.Edit) return;
            ItemType = ScheduleItemType.Task;

            if (!DueDate.HasValue)
            {
                DueDate = StartDate;
            }
            if (!DueTime.HasValue)
            {
                DueTime = new TimeSpan(23, 59, 0);
            }

            // 切换到任务时清空提醒
            Reminders.Clear();
        }

        [RelayCommand]
        private void GoBack()
        {
            // 编辑模式：返回到日期详情页；创建模式：返回主页
            if (Mode == EditMode.Edit && _sourceDate.HasValue)
            {
                _navigationService.NavigateToDayDetail(_sourceDate.Value);
            }
            else
            {
                _navigationService.NavigateToMain();
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (!ValidateInput())
            {
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = null;

                if (Mode == EditMode.Create)
                {
                    await CreateItemAsync();
                    // 创建成功后上传待上传的附件
                    await UploadPendingAttachmentsAsync();
                    // 只有日程才创建提醒
                    if (ItemType == ScheduleItemType.Event)
                    {
                        await CreateRemindersAsync();
                    }
                }
                else
                {
                    await UpdateItemAsync();
                    // TODO: 同步提醒的变更（只针对日程）
                }

                // 根据模式决定导航目标
                // 编辑模式：返回到日期详情页；创建模式：返回主页
                if (Mode == EditMode.Edit && _sourceDate.HasValue)
                {
                    _navigationService.NavigateToDayDetail(_sourceDate.Value);
                }
                else
                {
                    _navigationService.NavigateToMain();
                }
            }
            catch (ApiException ex)
            {
                ErrorMessage = $"保存失败: {ex.Message}";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"发生错误: {ex.Message}";
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 创建提醒（仅针对日程）
        /// </summary>
        private async Task CreateRemindersAsync()
        {
            if (!ItemId.HasValue || Reminders.Count == 0)
            {
                return;
            }

            foreach (var reminder in Reminders)
            {
                try
                {
                    // TODO: 调用 API 创建提醒
                    // await _api.CreateReminderAsync(CalendarId, ItemId.Value, reminder.OffsetMinutes);
                    Debug.WriteLine($"创建提醒: 提前 {reminder.OffsetMinutes} 分钟");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"创建提醒失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 上传创建模式下待上传的附件
        /// </summary>
        private async Task UploadPendingAttachmentsAsync()
        {
            if (!ItemId.HasValue || _pendingAttachmentPaths.Count == 0)
            {
                return;
            }

            foreach (var filePath in _pendingAttachmentPaths)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        await _attachmentService.UploadAttachmentAsync(ItemId.Value, filePath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"上传附件失败: {filePath}, 错误: {ex.Message}");
                    // 继续上传其他附件，不中断流程
                }
            }

            _pendingAttachmentPaths.Clear();
        }

        private static Window? GetHostWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }

            return null;
        }

        private static string BuildImportSummary(ImportOperationResult result)
        {
            if (result.SuccessCount > 0 && result.FailureCount == 0)
            {
                return $"导入成功 {result.SuccessCount} 条事件。";
            }

            var errorDetails = result.Errors.Select(FormatImportError);

            if (result.SuccessCount == 0)
            {
                return "导入失败: " + string.Join("；", errorDetails);
            }

            return $"导入成功 {result.SuccessCount} 条，失败 {result.FailureCount} 条: " + string.Join("；", errorDetails);
        }

        private static string FormatImportError(ImportValidationError error)
        {
            return error.Index > 0
                ? $"第 {error.Index} 条: {error.Message}"
                : error.Message;
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                ErrorMessage = "请输入标题";
                return false;
            }

            if (ItemType == ScheduleItemType.Event)
            {
                var startDateTime = StartDate.Add(StartTime);
                var endDateTime = EndDate.Add(EndTime);

                if (endDateTime <= startDateTime)
                {
                    ErrorMessage = "结束时间必须晚于开始时间";
                    return false;
                }
            }

            return true;
        }

        private static DateTimeOffset ToUtcDateTimeOffset(DateTime date, TimeSpan time)
        {
            var dateTime = date.Date.Add(time);
            var unspecifiedDateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
            return new DateTimeOffset(unspecifiedDateTime, TimeSpan.Zero);
        }

        private async Task CreateItemAsync()
        {
            var request = new CreateScheduleItemRequest(
                Type: ItemType == ScheduleItemType.Event ? "Event" : "Task",
                Title: Title.Trim(),
                Description: Description,
                Location: Location,
                StartAt: ItemType == ScheduleItemType.Event
                    ? ToUtcDateTimeOffset(StartDate, StartTime)
                    : null,
                EndAt: ItemType == ScheduleItemType.Event
                    ? ToUtcDateTimeOffset(EndDate, EndTime)
                    : null,
                DueAt: ItemType == ScheduleItemType.Task && DueDate.HasValue && DueTime.HasValue
                    ? ToUtcDateTimeOffset(DueDate.Value, DueTime.Value)
                    : null,
                AllDay: AllDay
            );

            var createdItem = await _api.ItemsPostAsync(CalendarId, request);
            ItemId = createdItem.Id; // 保存创建的 ID,以便上传附件和创建提醒
        }

        private async Task UpdateItemAsync()
        {
            if (!ItemId.HasValue) return;

            var request = new UpdateScheduleItemRequest(
                Type: ItemType == ScheduleItemType.Event ? "Event" : "Task",
                Title: Title.Trim(),
                Description: Description,
                Location: Location,
                StartAt: ItemType == ScheduleItemType.Event
                    ? ToUtcDateTimeOffset(StartDate, StartTime)
                    : null,
                EndAt: ItemType == ScheduleItemType.Event
                    ? ToUtcDateTimeOffset(EndDate, EndTime)
                    : null,
                DueAt: ItemType == ScheduleItemType.Task && DueDate.HasValue && DueTime.HasValue
                    ? ToUtcDateTimeOffset(DueDate.Value, DueTime.Value)
                    : null,
                AllDay: AllDay,
                IsCompleted: IsCompleted
            );
            await _api.ItemsPutAsync(CalendarId, ItemId.Value, request);
        }
    }

    /// <summary>
    /// 提醒视图模型
    /// </summary>
    public partial class ReminderViewModel : ObservableObject
    {
        [ObservableProperty]
        private Guid _id;

        [ObservableProperty]
        private int _offsetMinutes;

        public string DisplayText
        {
            get
            {
                if (OffsetMinutes == 0)
                    return "事件开始时";
                if (OffsetMinutes < 60)
                    return $"提前 {OffsetMinutes} 分钟";
                if (OffsetMinutes < 1440)
                    return $"提前 {OffsetMinutes / 60} 小时";
                return $"提前 {OffsetMinutes / 1440} 天";
            }
        }
    }
}