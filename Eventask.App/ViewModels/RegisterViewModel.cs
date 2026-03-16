using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eventask.App.Services;
using Eventask.App.Services.Generated;
using Eventask.Domain.Requests;
using Refit;

namespace Eventask.App.ViewModels;

public partial class RegisterViewModel : ViewModelBase
{
	private readonly IEventaskApi? _api;
	private readonly INavigationService? _navigation;
	private readonly IAuthService? _authService;

	[ObservableProperty]
	private string _username = string.Empty;

	[ObservableProperty]
	private string _password = string.Empty;

	[ObservableProperty]
	private string _confirmPassword = string.Empty;

	[ObservableProperty]
	private string _errorMessage = string.Empty;

	[ObservableProperty]
	private bool _hasError;

	[ObservableProperty]
	private bool _isLoading;

	// 设计时构造函数
	public RegisterViewModel ( )
	{
		// 设计时不需要实际功能
	}

	public RegisterViewModel ( IEventaskApi api, INavigationService navigation, IAuthService authService )
	{
		_api = api;
		_navigation = navigation;
		_authService = authService;
	}

	[RelayCommand]
	private async Task RegisterAsync ( )
	{
		// 设计时保护
		if ( _api == null || _navigation == null ) return;
		if ( IsLoading ) return;

		try
		{
			IsLoading = true;
			HasError = false;
			ErrorMessage = string.Empty;

			// 验证输入
			if ( string.IsNullOrWhiteSpace(Username) )
			{
				ShowError("请输入用户名");
				return;
			}

			if ( string.IsNullOrWhiteSpace(Password) )
			{
				ShowError("请输入密码");
				return;
			}

			if ( Password.Length < 6 )
			{
				ShowError("密码长度至少需要6个字符");
				return;
			}

			if ( Password != ConfirmPassword )
			{
				ShowError("两次输入的密码不一致");
				return;
			}

			var request = new RegisterRequest(Username, Password);
			var response = await _api.RegisterAsync(request);

			// 保存 token
			if (_authService != null)
			{
				await _authService.SetTokenAsync(response.AccessToken, response.ExpiresAt);
			}

			// 为新注册用户创建默认日历
			await CreateDefaultCalendarAsync();

			// 导航到主界面
			_navigation.NavigateToMain();
		}
		catch ( ApiException ex )
		{
			ShowError(ex.StatusCode switch
			{
				System.Net.HttpStatusCode.BadRequest => "用户名已存在或参数无效",
				_ => $"注册失败: {ex.Message}"
			});
		}
		catch ( Exception ex )
		{
			ShowError($"发生错误: {ex.Message}");
		}
		finally
		{
			IsLoading = false;
		}
	}

	private async Task CreateDefaultCalendarAsync ( )
	{
		try
		{
			var createRequest = new CreateCalendarRequest("我的日历");
			await _api!.CalendarsPostAsync(createRequest);
			Debug.WriteLine("已为新注册用户创建默认日历");
		}
		catch ( Exception ex )
		{
			// 静默失败,不影响注册流程
			Debug.WriteLine($"创建默认日历失败: {ex.Message}");
		}
	}

	[RelayCommand]
	private void NavigateToLogin ( )
	{
		// 设计时保护
		if ( _navigation == null ) return;
		
		_navigation.NavigateToLogin();
	}

	private void ShowError ( string message )
	{
		ErrorMessage = message;
		HasError = true;
	}
}