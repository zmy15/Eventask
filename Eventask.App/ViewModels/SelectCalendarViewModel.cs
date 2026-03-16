using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eventask.App.Models;
using Eventask.App.Services;
using Eventask.App.Services.Generated;
using Refit;

namespace Eventask.App.ViewModels
{
	public partial class SelectCalendarViewModel : ViewModelBase
	{
		private readonly IEventaskApi? _api;
		private readonly INavigationService? _navigationService;
		private readonly ICalendarStateService? _calendarStateService;

		[ObservableProperty]
		private string _errorMessage = string.Empty;

		[ObservableProperty]
		private bool _hasError;

		[ObservableProperty]
		private bool _isLoading;

		[ObservableProperty]
		private CalendarItemModel? _selectedCalendar;

		[ObservableProperty]
		private bool _isDeleting;

		public ObservableCollection<CalendarItemModel> Calendars { get; } = new();

		/// <summary>
		/// 是否只有一个日历(最后一个不能删除)
		/// </summary>
		public bool IsLastCalendar => Calendars.Count <= 1;

		public SelectCalendarViewModel()
		{
			// 设计时构造函数
		}

		public SelectCalendarViewModel(
			IEventaskApi api, 
			INavigationService navigationService,
			ICalendarStateService calendarStateService)
		{
			_api = api;
			_navigationService = navigationService;
			_calendarStateService = calendarStateService;
			_ = LoadCalendarsAsync();
		}

		private async Task LoadCalendarsAsync()
		{
			try
			{
				IsLoading = true;
				HasError = false;
				ErrorMessage = string.Empty;

				if (_api == null)
				{
					ShowError("API 服务未初始化");
					return;
				}

				var calendars = await _api.CalendarsGetAsync();

				Calendars.Clear();
				foreach (var calendar in calendars.Where(c => !c.IsDeleted))
				{
					Calendars.Add(CalendarItemModel.FromDto(calendar));
				}

				// 从服务加载当前选中的日历
				if (_calendarStateService != null && Calendars.Count > 0)
				{
					var currentId = _calendarStateService.CurrentCalendarId;
					SelectedCalendar = Calendars.FirstOrDefault(c => c.Id == currentId) ?? Calendars[0];
					SelectedCalendar.IsSelected = true;
				}
				else if (Calendars.Count > 0 && SelectedCalendar == null)
				{
					// 默认选中第一个
					SelectedCalendar = Calendars[0];
					SelectedCalendar.IsSelected = true;
				}

				// 通知 IsLastCalendar 属性变化
				OnPropertyChanged(nameof(IsLastCalendar));
			}
			catch (ApiException ex)
			{
				ShowError(ex.StatusCode switch
				{
					System.Net.HttpStatusCode.Unauthorized => "未授权,请重新登录",
					_ => $"加载日历失败: {ex.Message}"
				});
			}
			catch (Exception ex)
			{
				ShowError($"发生错误: {ex.Message}");
			}
			finally
			{
				IsLoading = false;
			}
		}

		[RelayCommand]
		private void SelectCalendar(CalendarItemModel calendar)
		{
			if (calendar == null)
				return;

			// 取消之前的选择
			if (SelectedCalendar != null)
			{
				SelectedCalendar.IsSelected = false;
			}

			// 选中新日历
			SelectedCalendar = calendar;
			SelectedCalendar.IsSelected = true;
		}

		[RelayCommand(CanExecute = nameof(CanDeleteCalendar))]
		private async Task DeleteCalendarAsync(CalendarItemModel calendar)
		{
			if (calendar == null)
				return;

			// 最后一个日历不能删除
			if (Calendars.Count <= 1)
			{
				ShowError("不能删除最后一个日历");
				return;
			}

			try
			{
				IsDeleting = true;
				HasError = false;
				ErrorMessage = string.Empty;

				if (_api == null)
				{
					ShowError("API 服务未初始化");
					return;
				}

				// 调用 API 删除日历
				await _api.CalendarsDeleteAsync(calendar.Id);

				// 如果删除的是当前选中的日历,选中另一个
				if (SelectedCalendar?.Id == calendar.Id)
				{
					SelectedCalendar.IsSelected = false;
					SelectedCalendar = null;
				}

				// 从列表中移除
				Calendars.Remove(calendar);

				// 如果没有选中的日历,自动选中第一个
				if (SelectedCalendar == null && Calendars.Count > 0)
				{
					SelectedCalendar = Calendars[0];
					SelectedCalendar.IsSelected = true;
				}

				// 通知 IsLastCalendar 属性变化
				OnPropertyChanged(nameof(IsLastCalendar));
			}
			catch (ApiException ex)
			{
				ShowError(ex.StatusCode switch
				{
					System.Net.HttpStatusCode.Forbidden => "没有权限删除此日历",
					System.Net.HttpStatusCode.NotFound => "日历不存在",
					System.Net.HttpStatusCode.Unauthorized => "未授权,请重新登录",
					_ => $"删除失败: {ex.Message}"
				});
			}
			catch (Exception ex)
			{
				ShowError($"发生错误: {ex.Message}");
			}
			finally
			{
				IsDeleting = false;
			}
		}

		private bool CanDeleteCalendar(CalendarItemModel? calendar)
		{
			// 只有在不是最后一个日历且不在删除中时才能删除
			return calendar != null && !IsDeleting && Calendars.Count > 1;
		}

		[RelayCommand]
		private async Task ConfirmAsync()
		{
			if (SelectedCalendar == null)
			{
				ShowError("请选择一个日历");
				return;
			}

			// 保存选中的日历到服务
			if (_calendarStateService != null)
			{
				await _calendarStateService.SetCurrentCalendarAsync(SelectedCalendar.Id);
			}

			_navigationService?.NavigateToMain();
		}

		[RelayCommand]
		private void Cancel()
		{
			_navigationService?.NavigateToMain();
		}

		private void ShowError(string message)
		{
			ErrorMessage = message;
			HasError = true;
		}

		partial void OnIsDeletingChanged(bool value)
		{
			// 当删除状态改变时,通知 DeleteCalendarCommand 可执行状态变化
			DeleteCalendarCommand.NotifyCanExecuteChanged();
		}
	}
}