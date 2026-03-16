using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Eventask.App.Models;
using Eventask.App.ViewModels;
using Eventask.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Eventask.App.Services;

public class NavigationService : INavigationService
{
	private readonly IServiceProvider _services;
	private MainViewModel? _cachedMainViewModel;
	private MainView? _cachedMainView;

	public NavigationService(IServiceProvider services)
	{
		_services = services;
	}

	public void NavigateTo(Control view)
	{
		if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			if (desktop.MainWindow != null)
			{
				desktop.MainWindow.Content = view;
			}
		}
		else if (App.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleView)
		{
			singleView.MainView = view;
		}
	}

	public void NavigateToMain()
	{
		// 如果已缓存 MainViewModel 和 MainView,重用以保持状态
		if (_cachedMainViewModel == null || _cachedMainView == null)
		{
			_cachedMainViewModel = _services.GetRequiredService<MainViewModel>();
			_cachedMainView = new MainView { DataContext = _cachedMainViewModel };
		}

		NavigateTo(_cachedMainView);
	}

	public void NavigateToLogin()
	{
		// 清空缓存,用户退出或登录时重置状态
		ClearMainViewCache();

		var loginViewModel = _services.GetRequiredService<LoginViewModel>();
		NavigateTo(new LoginView { DataContext = loginViewModel });
	}

	public void NavigateToRegister()
	{
		var registerViewModel = _services.GetRequiredService<RegisterViewModel>();
		NavigateTo(new RegisterView { DataContext = registerViewModel });
	}

	public void NavigateToCreateCalendar()
	{
		var createCalendarViewModel = _services.GetRequiredService<CreateCalendarViewModel>();
		NavigateTo(new CreateCalendarView { DataContext = createCalendarViewModel });
	}

	public void NavigateToSelectCalendar()
	{
		var selectCalendarViewModel = _services.GetRequiredService<SelectCalendarViewModel>();
		NavigateTo(new SelectCalendarView { DataContext = selectCalendarViewModel });
	}

	public void NavigateToSettings()
	{
		var vm = _services.GetRequiredService<SettingsViewModel>();
		_ = vm.LoadAsync();
		NavigateTo(new SettingsView { DataContext = vm });
	}

	public void NavigateToDayDetail(DateTime date)
	{
		var dayDetailViewModel = _services.GetRequiredService<DayDetailViewModel>();
		dayDetailViewModel.Initialize(date);
		NavigateTo(new DayDetailView { DataContext = dayDetailViewModel });
	}

	public void NavigateToEditScheduleItem(Guid calendarId, ScheduleItemType itemType, DateTime? defaultDate = null)
	{
		var viewModel = _services.GetRequiredService<EditScheduleItemViewModel>();
		viewModel.InitializeForCreate(calendarId, itemType, defaultDate);
		NavigateTo(new EditScheduleItemView { DataContext = viewModel });
	}

	public void NavigateToEditScheduleItem(ScheduleItemViewModel item, Guid calendarId, DateTime sourceDate)
	{
		var viewModel = _services.GetRequiredService<EditScheduleItemViewModel>();
		viewModel.InitializeForEdit(item, calendarId, sourceDate);
		NavigateTo(new EditScheduleItemView { DataContext = viewModel });
	}

	public void NavigateToCalendarMembers(Guid calendarId, string calendarName)
	{
		var viewModel = _services.GetRequiredService<CalendarMembersViewModel>();
		viewModel.Initialize(calendarId, calendarName);
		var view = new CalendarMembersView { DataContext = viewModel };
		NavigateTo(view);
	}

	public async Task<IReadOnlyList<RecognizedScheduleDraft>?> OpenNaturalLanguageDialogAsync(DateTime referenceDate)
	{
		var dialog = new NaturalLanguageDialog();
		var vm = _services.GetRequiredService<NaturalLanguageDialogViewModel>();
		vm.ReferenceDate = referenceDate.Date;
		dialog.DataContext = vm;

		if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
		{
			return await dialog.ShowDialog<IReadOnlyList<RecognizedScheduleDraft>?>(desktop.MainWindow);
		}

		// TODO: 处理其他 ApplicationLifetime 类型
		//return await dialog.ShowDialog<IReadOnlyList<RecognizedScheduleDraft>?>();

		return null;
	}

	/// <summary>
	/// 清除 MainView 缓存,用于登出或需要重置应用状态时
	/// </summary>
	private void ClearMainViewCache()
	{
		_cachedMainViewModel = null;
		_cachedMainView = null;
	}
}