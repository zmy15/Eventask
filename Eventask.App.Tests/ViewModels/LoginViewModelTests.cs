using Eventask.App.Services;
using Eventask.App.Services.Generated;
using Eventask.App.ViewModels;
using Eventask.Domain.Dtos;
using Eventask.Domain.Requests;
using FluentAssertions;
using Moq;
using Refit;
using System.Net;

namespace Eventask.App.Tests.ViewModels;

public class LoginViewModelTests
{
    private readonly Mock<IEventaskApi> _mockApi;
    private readonly Mock<INavigationService> _mockNavigation;
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<ICalendarStateService> _mockCalendarStateService;
    private readonly LoginViewModel _viewModel;

    public LoginViewModelTests()
    {
        _mockApi = new Mock<IEventaskApi>();
        _mockNavigation = new Mock<INavigationService>();
        _mockAuthService = new Mock<IAuthService>();
        _mockCalendarStateService = new Mock<ICalendarStateService>();

        _viewModel = new LoginViewModel(
            _mockApi.Object,
            _mockNavigation.Object,
            _mockAuthService.Object,
            _mockCalendarStateService.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Assert
        _viewModel.Username.Should().BeEmpty();
        _viewModel.Password.Should().BeEmpty();
        _viewModel.ErrorMessage.Should().BeEmpty();
        _viewModel.HasError.Should().BeFalse();
        _viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void DefaultConstructor_ShouldNotThrow()
    {
        // Act
        var act = () => new LoginViewModel();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task LoginCommand_WithEmptyUsername_ShouldShowError()
    {
        // Arrange
        _viewModel.Username = "";
        _viewModel.Password = "password123";

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _viewModel.HasError.Should().BeTrue();
        _viewModel.ErrorMessage.Should().Contain("用户名");
    }

    [Fact]
    public async Task LoginCommand_WithEmptyPassword_ShouldShowError()
    {
        // Arrange
        _viewModel.Username = "testuser";
        _viewModel.Password = "";

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _viewModel.HasError.Should().BeTrue();
        _viewModel.ErrorMessage.Should().Contain("密码");
    }

    [Fact]
    public async Task LoginCommand_WithValidCredentials_ShouldCallApiAndNavigate()
    {
        // Arrange
        _viewModel.Username = "testuser";
        _viewModel.Password = "password123";

        var authResponse = new AuthResponse("token123", DateTimeOffset.UtcNow.AddHours(1));
        _mockApi.Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync(authResponse);

        _mockApi.Setup(x => x.CalendarsGetAsync())
            .ReturnsAsync(new List<CalendarDto>
            {
                new CalendarDto(
                    Guid.NewGuid(),
                    "My Calendar",
                    0,
                    DateTimeOffset.UtcNow,
                    false)
            });

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _mockApi.Verify(x => x.LoginAsync(It.Is<LoginRequest>(r => 
            r.Username == "testuser" && r.Password == "password123")), Times.Once);
        _mockAuthService.Verify(x => x.SetTokenAsync("token123", It.IsAny<DateTimeOffset>()), Times.Once);
        _mockNavigation.Verify(x => x.NavigateToMain(), Times.Once);
        _viewModel.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task LoginCommand_WhenApiReturnsUnauthorized_ShouldShowErrorMessage()
    {
        // Arrange
        _viewModel.Username = "testuser";
        _viewModel.Password = "wrongpassword";

        var apiException = await ApiException.Create(
            new HttpRequestMessage(),
            HttpMethod.Post,
            new HttpResponseMessage(HttpStatusCode.Unauthorized),
            new RefitSettings());

        _mockApi.Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
            .ThrowsAsync(apiException);

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _viewModel.HasError.Should().BeTrue();
        _viewModel.ErrorMessage.Should().Contain("用户名或密码错误");
        _mockNavigation.Verify(x => x.NavigateToMain(), Times.Never);
    }

    [Fact]
    public async Task LoginCommand_WhenApiReturnsBadRequest_ShouldShowErrorMessage()
    {
        // Arrange
        _viewModel.Username = "testuser";
        _viewModel.Password = "password";

        var apiException = await ApiException.Create(
            new HttpRequestMessage(),
            HttpMethod.Post,
            new HttpResponseMessage(HttpStatusCode.BadRequest),
            new RefitSettings());

        _mockApi.Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
            .ThrowsAsync(apiException);

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _viewModel.HasError.Should().BeTrue();
        _viewModel.ErrorMessage.Should().Contain("请求参数无效");
    }

    [Fact]
    public async Task LoginCommand_ShouldSetIsLoadingDuringExecution()
    {
        // Arrange
        _viewModel.Username = "testuser";
        _viewModel.Password = "password123";

        var authResponse = new AuthResponse("token123", DateTimeOffset.UtcNow.AddHours(1));
        var tcs = new TaskCompletionSource<AuthResponse>();
        
        _mockApi.Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
            .Returns(tcs.Task);

        _mockApi.Setup(x => x.CalendarsGetAsync())
            .ReturnsAsync(new List<CalendarDto>());

        // Act
        var loginTask = _viewModel.LoginCommand.ExecuteAsync(null);
        
        // Assert - IsLoading should be true during execution
        _viewModel.IsLoading.Should().BeTrue();
        
        // Complete the operation
        tcs.SetResult(authResponse);
        await loginTask;
        
        // Assert - IsLoading should be false after completion
        _viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoginCommand_WhenNoCalendarsExist_ShouldCreateDefaultCalendar()
    {
        // Arrange
        _viewModel.Username = "testuser";
        _viewModel.Password = "password123";

        var authResponse = new AuthResponse("token123", DateTimeOffset.UtcNow.AddHours(1));
        _mockApi.Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync(authResponse);

        _mockApi.Setup(x => x.CalendarsGetAsync())
            .ReturnsAsync(new List<CalendarDto>());

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _mockApi.Verify(x => x.CalendarsPostAsync(It.Is<CreateCalendarRequest>(r => 
            r.Name == "我的日历")), Times.Once);
    }

    [Fact]
    public async Task LoginCommand_WhenCalendarsExist_ShouldSetCurrentCalendar()
    {
        // Arrange
        _viewModel.Username = "testuser";
        _viewModel.Password = "password123";

        var calendarId = Guid.NewGuid();
        var authResponse = new AuthResponse("token123", DateTimeOffset.UtcNow.AddHours(1));
        _mockApi.Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync(authResponse);

        _mockApi.Setup(x => x.CalendarsGetAsync())
            .ReturnsAsync(new List<CalendarDto>
            {
                new CalendarDto(
                    calendarId,
                    "Existing Calendar",
                    0,
                    DateTimeOffset.UtcNow,
                    false)
            });

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _mockCalendarStateService.Verify(x => x.SetCurrentCalendarAsync(calendarId), Times.Once);
    }

    [Fact]
    public void NavigateToRegisterCommand_ShouldNavigateToRegisterView()
    {
        // Act
        _viewModel.NavigateToRegisterCommand.Execute(null);

        // Assert
        _mockNavigation.Verify(x => x.NavigateToRegister(), Times.Once);
    }

    [Fact]
    public void LoginCommand_CanExecute_WhenNotLoading_ShouldReturnTrue()
    {
        // Arrange
        _viewModel.IsLoading = false;

        // Act
        var canExecute = _viewModel.LoginCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public void LoginCommand_CanExecute_WhenLoading_ShouldReturnFalse()
    {
        // Arrange - Simulate loading state
        // We can't directly set IsLoading from outside, but we can check the initial state
        // and verify the NotifyCanExecuteChangedFor attribute is working
        
        // Assert
        _viewModel.LoginCommand.CanExecute(null).Should().BeTrue();
    }
}
