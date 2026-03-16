using Eventask.App.Models;
using Eventask.App.Services;
using Eventask.App.Services.Generated;
using Eventask.App.ViewModels;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Eventask.App.Tests.ViewModels;

public class MainViewModelTests
{
    private readonly Mock<INavigationService> _mockNavigation;
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<ICalendarStateService> _mockCalendarStateService;
    private readonly Mock<IEventaskApi> _mockApi;
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _mockNavigation = new Mock<INavigationService>();
        _mockAuthService = new Mock<IAuthService>();
        _mockCalendarStateService = new Mock<ICalendarStateService>();
        _mockApi = new Mock<IEventaskApi>();

        var holidayOptions = Options.Create(new HolidayOptions());
        var refreshService = new CalendarItemRefreshService();

        _viewModel = new MainViewModel(
            _mockNavigation.Object,
            _mockAuthService.Object,
            _mockCalendarStateService.Object,
            refreshService,
            holidayOptions,
            _mockApi.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Assert
        _viewModel.CurrentDate.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
        _viewModel.SelectedDate.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
        _viewModel.CurrentMode.Should().Be(CalendarMode.Month);
        _viewModel.IsSearchVisible.Should().BeFalse();
        _viewModel.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void DefaultConstructor_ShouldInitializeYearGroups()
    {
        // Arrange & Act
        var viewModel = new MainViewModel();

        // Assert
        viewModel.YearGroups.Should().NotBeEmpty();
        // Should contain current year, previous year, and next year
        viewModel.YearGroups.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public void YearHeaderText_ShouldFormatYearCorrectly()
    {
        // Arrange
        _viewModel.CurrentDate = new DateTime(2024, 6, 15);

        // Act
        var headerText = _viewModel.YearHeaderText;

        // Assert
        headerText.Should().Be("2024年");
    }

    [Fact]
    public void MonthHeaderText_ShouldFormatMonthCorrectly()
    {
        // Arrange
        _viewModel.CurrentDate = new DateTime(2024, 6, 15);

        // Act
        var headerText = _viewModel.MonthHeaderText;

        // Assert
        headerText.Should().Be("6月");
    }

    [Fact]
    public void FullDateHeaderText_ShouldFormatFullYearCorrectly()
    {
        // Arrange
        _viewModel.CurrentDate = new DateTime(2024, 6, 15);

        // Act
        var headerText = _viewModel.FullDateHeaderText;

        // Assert
        headerText.Should().Be("2024年");
    }

    [Fact]
    public void CurrentDate_WhenChanged_ShouldNotifyHeaderTextProperties()
    {
        // Arrange
        var propertyChangedEvents = new List<string>();
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
                propertyChangedEvents.Add(e.PropertyName);
        };

        // Act
        _viewModel.CurrentDate = new DateTime(2025, 12, 31);

        // Assert
        propertyChangedEvents.Should().Contain("CurrentDate");
        propertyChangedEvents.Should().Contain("YearHeaderText");
        propertyChangedEvents.Should().Contain("MonthHeaderText");
        propertyChangedEvents.Should().Contain("FullDateHeaderText");
    }

    [Fact]
    public void SelectedDate_CanBeSetAndRetrieved()
    {
        // Arrange
        var testDate = new DateTime(2024, 12, 25);

        // Act
        _viewModel.SelectedDate = testDate;

        // Assert
        _viewModel.SelectedDate.Should().Be(testDate);
    }

    [Fact]
    public void CurrentMode_CanBeSetAndRetrieved()
    {
        // Arrange & Act
        _viewModel.CurrentMode = CalendarMode.Year;

        // Assert
        _viewModel.CurrentMode.Should().Be(CalendarMode.Year);

        // Act
        _viewModel.CurrentMode = CalendarMode.Day;

        // Assert
        _viewModel.CurrentMode.Should().Be(CalendarMode.Day);
    }

    [Fact]
    public void IsSearchVisible_CanBeToggled()
    {
        // Arrange
        _viewModel.IsSearchVisible = false;

        // Act
        _viewModel.IsSearchVisible = true;

        // Assert
        _viewModel.IsSearchVisible.Should().BeTrue();

        // Act
        _viewModel.IsSearchVisible = false;

        // Assert
        _viewModel.IsSearchVisible.Should().BeFalse();
    }

    [Fact]
    public void SearchText_CanBeSetAndRetrieved()
    {
        // Arrange
        var searchText = "test search";

        // Act
        _viewModel.SearchText = searchText;

        // Assert
        _viewModel.SearchText.Should().Be(searchText);
    }

    [Fact]
    public void MonthViewDays_ShouldBeInitialized()
    {
        // Assert
        _viewModel.MonthViewDays.Should().NotBeNull();
    }

    [Theory]
    [InlineData(CalendarMode.Year)]
    [InlineData(CalendarMode.Month)]
    [InlineData(CalendarMode.Day)]
    public void CurrentMode_AllModes_ShouldBeSettable(CalendarMode mode)
    {
        // Act
        _viewModel.CurrentMode = mode;

        // Assert
        _viewModel.CurrentMode.Should().Be(mode);
    }

    [Fact]
    public void ViewModel_WithNullServices_ShouldStillInitializeBasicProperties()
    {
        // Arrange & Act
        var viewModel = new MainViewModel(null!, null!, null!, new CalendarItemRefreshService(), Options.Create(new HolidayOptions()), new Mock<IEventaskApi>().Object);

        // Assert
        viewModel.CurrentDate.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
        viewModel.YearGroups.Should().NotBeEmpty();
    }

    [Fact]
    public void YearGroups_ShouldContainCurrentYear()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;

        // Act & Assert
        _viewModel.YearGroups.Should().Contain(y => y.Year == currentYear);
    }

    [Fact]
    public void YearGroups_ShouldContainPreviousYear()
    {
        // Arrange
        var previousYear = DateTime.Now.Year - 1;

        // Act & Assert
        _viewModel.YearGroups.Should().Contain(y => y.Year == previousYear);
    }

    [Fact]
    public void YearGroups_ShouldContainNextYear()
    {
        // Arrange
        var nextYear = DateTime.Now.Year + 1;

        // Act & Assert
        _viewModel.YearGroups.Should().Contain(y => y.Year == nextYear);
    }

    [Fact]
    public void PropertyChanged_WhenCurrentDateChanges_ShouldRaiseEvent()
    {
        // Arrange
        var eventRaised = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "CurrentDate")
                eventRaised = true;
        };

        // Act
        _viewModel.CurrentDate = DateTime.Now.AddDays(1);

        // Assert
        eventRaised.Should().BeTrue();
    }
}
