using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eventask.App.Services;
using Eventask.App.Services.Generated;
using Eventask.Domain.Requests;
using Refit;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Eventask.App.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
	private readonly IEventaskApi? _api;
	private readonly INavigationService? _navigation;
	private readonly IAuthService? _authService;
	private readonly ICalendarStateService? _calendarStateService;

	[ObservableProperty]
	private string _username = string.Empty;

	[ObservableProperty]
	private string _password = string.Empty;

	[ObservableProperty]
	private string _errorMessage = string.Empty;

	[ObservableProperty]
	private bool _hasError;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(LoginCommand))]
	private bool _isLoading;

	public LoginViewModel()
	{
		// 设计时不需要实际功能
	}

	public LoginViewModel(
		IEventaskApi api,
		INavigationService navigation,
		IAuthService authService,
		ICalendarStateService calendarStateService)
	{
		_api = api;
		_navigation = navigation;
		_authService = authService;
		_calendarStateService = calendarStateService;
		// 调试输出
		Debug.WriteLine($"LoginViewModel 创建: API={_api != null}, Navigation={_navigation}");
	}

	// 添加 CanExecute 方法
	private bool CanLogin() => !IsLoading && _api != null && _navigation != null;

	[RelayCommand(CanExecute = nameof(CanLogin))]
	private async Task LoginAsync()
	{
		try
		{
			IsLoading = true;
			HasError = false;
			ErrorMessage = string.Empty;

			if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
			{
				ShowError("请输入用户名和密码");
				return;
			}

			var request = new LoginRequest(Username, Password);
			var response = await _api!.LoginAsync(request);

			// 保存 token
			if (_authService != null)
			{
				await _authService.SetTokenAsync(response.AccessToken, response.ExpiresAt);
			}

			// 检查用户是否有日历,如果没有则创建默认日历
			await EnsureDefaultCalendarExistsAsync();

			// 加载当前日历ID(登录后立即加载第一个可用日历)
			if (_calendarStateService != null)
			{
				try
				{
					var calendars = await _api.CalendarsGetAsync();
					var firstCalendar = calendars.FirstOrDefault(c => !c.IsDeleted);
					if (firstCalendar != null)
					{
						await _calendarStateService.SetCurrentCalendarAsync(firstCalendar.Id);
						Debug.WriteLine($"已设置当前日历: {firstCalendar.Id}");
					}
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"加载日历ID失败: {ex.Message}");
				}
			}

			// 导航到主界面
			_navigation!.NavigateToMain();
		}
		catch (ApiException ex)
		{
			ShowError(ex.StatusCode switch
			{
				System.Net.HttpStatusCode.Unauthorized => "用户名或密码错误",
				System.Net.HttpStatusCode.BadRequest => "请求参数无效",
				_ => $"登录失败: {ex.Message}"
			});
		}
		catch (Exception ex)
		{
			ShowError($"发生错误: {ex.Message}");
			Debug.WriteLine(ex.Message);
		}
		finally
		{
			IsLoading = false;
		}
	}

	private async Task EnsureDefaultCalendarExistsAsync()
	{
		try
		{
			// 获取用户的日历列表
			var calendars = await _api!.CalendarsGetAsync();

			// 如果用户没有任何日历,创建一个默认日历
			if (calendars == null || calendars.Count == 0)
			{
				var createRequest = new CreateCalendarRequest("我的日历");
				await _api.CalendarsPostAsync(createRequest);
				Debug.WriteLine("已为用户创建默认日历");
			}
		}
		catch (Exception ex)
		{
			// 静默失败,不影响登录流程
			Debug.WriteLine($"创建默认日历失败: {ex.Message}");
		}
	}

	[RelayCommand]
	private void NavigateToRegister()
	{
		// 设计时保护
		if (_navigation == null) return;

		_navigation.NavigateToRegister();
	}

	private void ShowError(string message)
	{
		ErrorMessage = message;
		HasError = true;
	}
}