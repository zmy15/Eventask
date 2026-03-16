using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eventask.App.Models;
using Eventask.App.Services;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Eventask.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ILocalStorageService _localStorage;
    private readonly IOptions<NlpOptions> _options;
    private readonly INavigationService _navigationService;
    private readonly IAuthService _authService;
    private readonly ICalendarStateService _calendarStateService;

    [ObservableProperty]
    private string? _apiKey;

    [ObservableProperty]
    private string? _endpoint;

    [ObservableProperty]
    private string? _model;

    [ObservableProperty]
    private string? _status;

    public SettingsViewModel(
        ILocalStorageService localStorage,
        IOptions<NlpOptions> options,
        INavigationService navigationService,
        IAuthService authService,
        ICalendarStateService calendarStateService)
    {
        _localStorage = localStorage;
        _options = options;
        _navigationService = navigationService;
        _authService = authService;
        _calendarStateService = calendarStateService;
    }

    public async Task LoadAsync()
    {
        try
        {
            var defaults = _options.Value;
            ApiKey = await _localStorage.GetAsync(NlpStorageKeys.ApiKey) ?? defaults.ApiKey ?? string.Empty;
            Endpoint = await _localStorage.GetAsync(NlpStorageKeys.Endpoint) ?? defaults.Endpoint ?? string.Empty;
            Model = await _localStorage.GetAsync(NlpStorageKeys.Model) ?? defaults.Model ?? string.Empty;
            Status = "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载设置失败: {ex.Message}");
            Status = "加载失败";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            await PersistAsync(NlpStorageKeys.ApiKey, ApiKey);
            await PersistAsync(NlpStorageKeys.Endpoint, Endpoint);
            await PersistAsync(NlpStorageKeys.Model, Model);
            Status = "已保存";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"保存设置失败: {ex.Message}");
            Status = "保存失败";
        }
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        try
        {
            await _localStorage.RemoveAsync(NlpStorageKeys.ApiKey);
            await _localStorage.RemoveAsync(NlpStorageKeys.Endpoint);
            await _localStorage.RemoveAsync(NlpStorageKeys.Model);
            await LoadAsync();
            Status = "已清除";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清除设置失败: {ex.Message}");
            Status = "清除失败";
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        try
        {
            await _authService.ClearTokenAsync();
            await _calendarStateService.ClearCurrentCalendarAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"退出登录时发生错误: {ex.Message}");
        }
        finally
        {
            _navigationService.NavigateToLogin();
        }
    }

    [RelayCommand]
    private void Back()
    {
        _navigationService.NavigateToMain();
    }

    private async Task PersistAsync(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            await _localStorage.RemoveAsync(key);
        }
        else
        {
            await _localStorage.SetAsync(key, value);
        }
    }
}
