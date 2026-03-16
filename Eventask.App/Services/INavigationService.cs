using Avalonia.Controls;
using Eventask.App.Models;
using Eventask.App.ViewModels;
using System;

namespace Eventask.App.Services;

public interface INavigationService
{
    void NavigateTo(Control view);
    void NavigateToMain();
    void NavigateToLogin();
    void NavigateToRegister();
    void NavigateToCreateCalendar();
    void NavigateToSelectCalendar();
    void NavigateToSettings();
    void NavigateToDayDetail(DateTime date);
    void NavigateToEditScheduleItem(Guid calendarId, ScheduleItemType itemType, DateTime? defaultDate = null);
    void NavigateToEditScheduleItem(ScheduleItemViewModel item, Guid calendarId, DateTime sourceDate);
    void NavigateToCalendarMembers(Guid calendarId, string calendarName);
    Task<IReadOnlyList<RecognizedScheduleDraft>?> OpenNaturalLanguageDialogAsync(DateTime referenceDate);
}