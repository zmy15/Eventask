using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eventask.App.Services;
using Eventask.App.Services.Generated;
using Eventask.Domain.Requests;
using Refit;

namespace Eventask.App.ViewModels
{
    public partial class CreateCalendarViewModel : ViewModelBase
    {
        private readonly IEventaskApi? _api;
        private readonly INavigationService? _navigationService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
        private string _calendarName = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
        private bool _isLoading = false;

        public CreateCalendarViewModel ( )
        {
            // 设计时构造函数
        }

        public CreateCalendarViewModel (IEventaskApi api, INavigationService navigationService)
        {
            _api = api;
            _navigationService = navigationService;
        }

        private bool CanCreate ( ) => !IsLoading && !string.IsNullOrWhiteSpace(CalendarName);

        [RelayCommand(CanExecute = nameof(CanCreate))]
        private async Task CreateAsync ( )
        {
            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = string.Empty;

                if ( _api == null )
                {
                    ShowError("API 服务未初始化");
                    return;
                }

                var request = new CreateCalendarRequest(CalendarName.Trim());
                var result = await _api.CalendarsPostAsync(request);

                // 创建成功,返回主界面
                _navigationService?.NavigateToMain();
            }
            catch ( ApiException ex )
            {
                ShowError(ex.StatusCode switch
                {
                    System.Net.HttpStatusCode.BadRequest => "日历名称无效",
                    System.Net.HttpStatusCode.Unauthorized => "未授权,请重新登录",
                    _ => $"创建失败: {ex.Message}"
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

        [RelayCommand]
        private void Cancel ( )
        {
            _navigationService?.NavigateToMain();
        }

        private void ShowError (string message)
        {
            ErrorMessage = message;
            HasError = true;
        }
    }
}