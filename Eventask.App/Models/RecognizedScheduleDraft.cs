using CommunityToolkit.Mvvm.ComponentModel;
using Eventask.App.ViewModels;

namespace Eventask.App.Models;

public partial class RecognizedScheduleDraft : ObservableObject
{
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    public ScheduleItemType ItemType
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsEvent));
                OnPropertyChanged(nameof(IsTask));
            }
        }
    } = ScheduleItemType.Event;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _location;

    [ObservableProperty]
    private DateTimeOffset? _startDate;

    [ObservableProperty]
    private TimeSpan? _startTime;

    [ObservableProperty]
    private DateTimeOffset? _endDate;

    [ObservableProperty]
    private TimeSpan? _endTime;

    [ObservableProperty]
    private DateTimeOffset? _dueDate;

    [ObservableProperty]
    private TimeSpan? _dueTime;

    [ObservableProperty]
    private bool _allDay;

    public bool IsEvent => ItemType == ScheduleItemType.Event;
    public bool IsTask => ItemType == ScheduleItemType.Task;

    public RecognizedScheduleDraft Clone()
    {
        return new RecognizedScheduleDraft
        {
            Id = Id,
            ItemType = ItemType,
            Title = Title,
            Description = Description,
            Location = Location,
            StartDate = StartDate,
            StartTime = StartTime,
            EndDate = EndDate,
            EndTime = EndTime,
            DueDate = DueDate,
            DueTime = DueTime,
            AllDay = AllDay
        };
    }
}
