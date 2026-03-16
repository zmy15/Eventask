using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eventask.App.Services;
using Eventask.App.Services.Generated;
using Eventask.Domain.Requests;
using Refit;

namespace Eventask.App.ViewModels
{
	public partial class DayDetailViewModel : ObservableObject
	{
		private readonly INavigationService _navigationService;
		private readonly IEventaskApi _api;
		private readonly ICalendarStateService _calendarStateService;
		private readonly ICalendarItemRefreshService _calendarItemRefreshService;

		[ObservableProperty]
		private DateTime _selectedDate;

		[ObservableProperty]
		private string _dateHeaderText = string.Empty;

		[ObservableProperty]
		private Guid _currentCalendarId = Guid.Empty;

		public ObservableCollection<ScheduleItemViewModel> Events { get; } = new();
		public ObservableCollection<ScheduleItemViewModel> Tasks { get; } = new();

		/// <summary>
		/// 判断是否有日程或任务
		/// </summary>
		public bool HasSchedule => Events.Count > 0 || Tasks.Count > 0;

		public DayDetailViewModel(
			INavigationService navigationService,
			IEventaskApi api,
         ICalendarStateService calendarStateService,
			ICalendarItemRefreshService calendarItemRefreshService)
		{
			_navigationService = navigationService;
			_api = api;
			_calendarStateService = calendarStateService;
           _calendarItemRefreshService = calendarItemRefreshService;
		}

		public async void Initialize(DateTime date)
		{
			SelectedDate = date;
			DateHeaderText = $"{date:yyyy年M月d日 dddd}";

			// 从服务加载当前日历ID
			await EnsureCalendarIdAsync();

			// 加载日程数据
			await LoadScheduleItemsAsync();
		}

		/// <summary>
		/// 确保已加载日历ID,从 CalendarStateService 获取
		/// </summary>
		private async Task EnsureCalendarIdAsync()
		{
			try
			{
				// 从服务获取当前日历ID
				var calendarId = await _calendarStateService.EnsureCalendarSelectedAsync();

				if (calendarId != Guid.Empty)
				{
					CurrentCalendarId = calendarId;
				}
			}
			catch (ApiException)
			{
				// TODO: 处理错误 - 可能需要导航到选择日历页面
			}
		}

		[RelayCommand]
		private void GoBack()
		{
			_navigationService.NavigateToMain();
		}

		[RelayCommand]
		private void AddEvent()
		{
			_navigationService.NavigateToEditScheduleItem(
				calendarId: CurrentCalendarId,
				itemType: ScheduleItemType.Event,
				defaultDate: SelectedDate);
		}

		[RelayCommand]
		private void AddTask()
		{
			_navigationService.NavigateToEditScheduleItem(
				calendarId: CurrentCalendarId,
				itemType: ScheduleItemType.Task,
				defaultDate: SelectedDate);
		}

		[RelayCommand]
		private void EditItem(ScheduleItemViewModel item)
		{
			if (item == null)
				return;
			// 传递当前选中的日期
			_navigationService.NavigateToEditScheduleItem(item, CurrentCalendarId, SelectedDate);
		}

		[RelayCommand]
		private async Task DeleteItemAsync(ScheduleItemViewModel item)
		{
			if (item == null)
				return;

			try
			{
				// 修正: 添加 calendarId 参数
				await _api.ItemsDeleteAsync(CurrentCalendarId, item.Id);

				if (item.IsTask)
				{
					Tasks.Remove(item);
				}
				else
				{
					Events.Remove(item);
				}

				// 删除后通知 HasSchedule 更新
				OnPropertyChanged(nameof(HasSchedule));
               _calendarItemRefreshService.NotifyMonthItemsChanged(SelectedDate);
			}
			catch (ApiException ex)
			{
				// TODO: 显示错误消息
			}
		}

		[RelayCommand]
		private async Task ToggleTaskCompleteAsync(ScheduleItemViewModel task)
		{
			if (task == null || !task.IsTask)
				return;

			// 保存原状态用于回滚
			var originalState = task.IsCompleted;

			try
			{
				// 乐观更新 UI
				task.IsCompleted = !task.IsCompleted;

				// 调用API更新完成状态
				var updateRequest = new UpdateScheduleItemRequest(
					Type: "Task",
					Title: task.Title,
					Description: task.Description,
					Location: task.Location,
					StartAt: null,
					EndAt: null,
					DueAt: task.DueTime.HasValue
						? new DateTimeOffset(SelectedDate.Date.Add(task.DueTime.Value), TimeSpan.Zero)
						: null,
					AllDay: false,
					IsCompleted: task.IsCompleted
				);

				// 修正: 添加 calendarId 参数和 body 参数
				await _api.ItemsPutAsync(CurrentCalendarId, task.Id, updateRequest);
               _calendarItemRefreshService.NotifyMonthItemsChanged(SelectedDate);
			}
			catch (ApiException ex)
			{
				// 回滚状态
				task.IsCompleted = originalState;
				// TODO: 显示错误消息
				System.Diagnostics.Debug.WriteLine($"更新任务状态失败: {ex.Message}");
			}
		}

		private async Task LoadScheduleItemsAsync()
		{
			Events.Clear();
			Tasks.Clear();

			if (CurrentCalendarId == Guid.Empty)
			{
				// 清空后立即通知
				OnPropertyChanged(nameof(HasSchedule));
				return;
			}

			try
			{
				// 使用 UTC 时区创建 DateTimeOffset,避免时区偏移问题
				var selectedDateUtc = DateTime.SpecifyKind(SelectedDate.Date, DateTimeKind.Unspecified);
				var startDate = new DateTimeOffset(selectedDateUtc, TimeSpan.Zero);
				var endDate = startDate.AddDays(1);

				var items = await _api.ItemsGetAsync(CurrentCalendarId, from: startDate, to: endDate);

				foreach (var item in items.Where(i => !i.IsDeleted))
				{
					var viewModel = new ScheduleItemViewModel
					{
						Id = item.Id,
						Title = item.Title,
						Description = item.Description,
						Location = item.Location,
						IsTask = item.Type == "Task"
					};

					if (item.Type == "Task")
					{
						viewModel.DueTime = item.DueAt?.DateTime.TimeOfDay;
						viewModel.IsCompleted = item.IsCompleted;
						Tasks.Add(viewModel);
					}
					else
					{
						viewModel.StartTime = item.StartAt?.DateTime.TimeOfDay;
						viewModel.EndTime = item.EndAt?.DateTime.TimeOfDay;
						Events.Add(viewModel);
					}
				}

				// 加载完成后手动通知 HasSchedule 属性更新
				OnPropertyChanged(nameof(HasSchedule));
			}
			catch (ApiException ex)
			{
				// TODO: 显示错误消息
				// 即使出错也要通知 UI 更新
				OnPropertyChanged(nameof(HasSchedule));
			}
		}

		[RelayCommand]
		private async Task RecognizeWithAiAsync()
		{
			await EnsureCalendarIdAsync();
			if (CurrentCalendarId == Guid.Empty)
			{
				return;
			}

			var drafts = await _navigationService.OpenNaturalLanguageDialogAsync(SelectedDate);
			if (drafts == null || drafts.Count == 0)
			{
				return; // 用户取消或没有结果
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

						await _api.ItemsPostAsync(CurrentCalendarId, request);
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

						await _api.ItemsPostAsync(CurrentCalendarId, request);
					}
				}
				catch (ApiException ex)
				{
					Debug.WriteLine($"创建草稿失败: {ex.Message}");
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"处理草稿时发生错误: {ex.Message}");
				}
			}

			// 创建完成后刷新列表
			await LoadScheduleItemsAsync();
		}

		private static DateTimeOffset ToUtcDateTimeOffset(DateTimeOffset date, TimeSpan time)
		{
			var dateTime = date.Date.Add(time);
			var unspecifiedDateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
			return new DateTimeOffset(unspecifiedDateTime, TimeSpan.Zero);
		}
	}

	public partial class ScheduleItemViewModel : ObservableObject
	{
		[ObservableProperty]
		private Guid _id;

		[ObservableProperty]
		private string _title = string.Empty;

		[ObservableProperty]
		private string? _description;

		[ObservableProperty]
		private string? _location;

		[ObservableProperty]
		private TimeSpan? _startTime;

		[ObservableProperty]
		private TimeSpan? _endTime;

		[ObservableProperty]
		private TimeSpan? _dueTime;

		[ObservableProperty]
		private bool _isCompleted;

		[ObservableProperty]
		private bool _isTask;

		public string TimeRangeText
		{
			get
			{
				if (IsTask && DueTime.HasValue)
				{
					return $"截止时间: {DueTime.Value:hh\\:mm}";
				}
				if (StartTime.HasValue && EndTime.HasValue)
				{
					return $"{StartTime.Value:hh\\:mm} - {EndTime.Value:hh\\:mm}";
				}
				return string.Empty;
			}
		}

		public string ItemTypeText => IsTask ? "任务" : "日程";
	}
}