using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eventask.App.Services;
using Eventask.App.Services.Generated;
using Eventask.Domain.Dtos;
using Eventask.Domain.Entity.Calendars;
using Eventask.Domain.Requests;
using Refit;

namespace Eventask.App.ViewModels;

public partial class CalendarMembersViewModel : ViewModelBase
{
    private readonly IEventaskApi? _api;
    private readonly INavigationService? _navigationService;

    [ObservableProperty]
    private Guid _calendarId;

    [ObservableProperty]
    private string _calendarName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CalendarMemberItemViewModel> _members = [];

    [ObservableProperty]
    private string _newMemberUsername = string.Empty;

    [ObservableProperty]
    private string _selectedRole = "Editor";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddMemberCommand))]
    private bool _isLoading;

    public string[ ] AvailableRoles { get; } = ["Editor", "Viewer"];

    public CalendarMembersViewModel ( )
    {
        // 设计时构造函数
    }

    public CalendarMembersViewModel (IEventaskApi api, INavigationService navigationService)
    {
        _api = api;
        _navigationService = navigationService;
    }

    public void Initialize (Guid calendarId, string calendarName)
    {
        CalendarId = calendarId;
        CalendarName = calendarName;
        _ = LoadMembersAsync();
    }

    [RelayCommand]
    private async Task LoadMembersAsync ( )
    {
        if ( _api is null )
            return;

        try
        {
            IsLoading = true;
            HasError = false;

            var members = await _api.MembersGetAsync(CalendarId);
            Members.Clear();

            if (members is IEnumerable<MemberDto> memberList)
            {
                foreach (var member in memberList)
                {
                    // 将 CalendarMemberRole 枚举转换为字符串
                    Members.Add(new CalendarMemberItemViewModel
                    {
                        Id = member.Id,
                        UserId = member.UserId,
                        Username = member.Username,
                        Role = member.Role.ToString(),
                        IsOwner = member.Role == CalendarMemberRole.Owner
                    });
                }
            }
        }
        catch ( ApiException ex )
        {
            ShowError($"加载成员失败: {ex.Message}");
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

    private bool CanAddMember ( ) => !IsLoading;

    [RelayCommand(CanExecute = nameof(CanAddMember))]
    private async Task AddMemberAsync ( )
    {
        if ( _api is null )
            return;
        if (string.IsNullOrWhiteSpace(NewMemberUsername))
        {
            ShowError("用户名不能为空!");
            return;
        }

        try
        {
            IsLoading = true;
            HasError = false;

            var request = new AddCalendarMemberRequest(NewMemberUsername.Trim(), Enum.Parse<CalendarMemberRole>(SelectedRole));
            await _api.MembersPostAsync(CalendarId, request);

            NewMemberUsername = string.Empty;
            await LoadMembersAsync();
        }
        catch ( ApiException ex )
        {
            ShowError(ex.StatusCode switch
            {
                System.Net.HttpStatusCode.NotFound => $"用户 '{NewMemberUsername}' 不存在",
                System.Net.HttpStatusCode.BadRequest => "无效的请求",
                System.Net.HttpStatusCode.Forbidden => "您没有权限添加成员",
                System.Net.HttpStatusCode.Conflict => "操作冲突，请重试",
                _ => $"添加成员失败: {ex.Message}"
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
    private async Task RemoveMemberAsync (CalendarMemberItemViewModel? member)
    {
        if ( _api is null || member is null || member.IsOwner )
            return;

        try
        {
            IsLoading = true;
            HasError = false;

            await _api.MembersDeleteAsync(CalendarId, member.UserId);
            Members.Remove(member);
        }
        catch ( ApiException ex )
        {
            ShowError(ex.StatusCode switch
            {
                System.Net.HttpStatusCode.Forbidden => "您没有权限移除成员",
                System.Net.HttpStatusCode.NotFound => "成员不存在",
                _ => $"移除成员失败: {ex.Message}"
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
    private void GoBack ( )
    {
        _navigationService?.NavigateToMain();
    }

    private void ShowError (string message)
    {
        ErrorMessage = message;
        HasError = true;
    }
}

public partial class CalendarMemberItemViewModel : ObservableObject
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _role = string.Empty;

    [ObservableProperty]
    private bool _isOwner;

    public string RoleDisplay => Role switch
    {
        "Owner" => "所有者",
        "Editor" => "编辑者",
        "Viewer" => "查看者",
        _ => Role
    };
}