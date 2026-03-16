using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Eventask.App.Models;
using Eventask.App.Services;

namespace Eventask.App.ViewModels;

public partial class NaturalLanguageDialogViewModel : ObservableObject
{
    private readonly INaturalLanguageParser _parser;

    [ObservableProperty]
    private string _promptText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    private bool _isParsing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    private string? _errorMessage;

    public ObservableCollection<RecognizedScheduleDraft> Drafts { get; } = new();

    public DateTime ReferenceDate { get; set; } = DateTime.Today;

    public bool CanApply => !IsParsing && Drafts.Count > 0;

    public NaturalLanguageDialogViewModel (INaturalLanguageParser parser)
    {
        _parser = parser;
    }

    public NaturalLanguageDialogViewModel ( )
    {

    }

    [RelayCommand]
    private async Task ParseAsync ( )
    {
        if ( string.IsNullOrWhiteSpace(PromptText) )
        {
            ErrorMessage = "请输入需要识别的内容";
            return;
        }

        try
        {
            IsParsing = true;
            ErrorMessage = null;
            Drafts.Clear();

            var results = await _parser.ParseAsync(PromptText.Trim(), ReferenceDate);
            foreach ( var draft in results )
            {
                Drafts.Add(draft);
            }
        }
        catch ( Exception ex )
        {
            ErrorMessage = $"识别失败: {ex.Message}";
        }
        finally
        {
            IsParsing = false;
        }

        OnPropertyChanged(nameof(CanApply));
    }

    [RelayCommand]
    private void RemoveDraft (RecognizedScheduleDraft? draft)
    {
        if ( draft == null )
            return;
        Drafts.Remove(draft);
        OnPropertyChanged(nameof(CanApply));
    }

    [RelayCommand]
    private void AddBlankDraft ( )
    {
        Drafts.Add(new RecognizedScheduleDraft
        {
            ItemType = ScheduleItemType.Event,
            Title = "",
            StartDate = ReferenceDate.Date,
            EndDate = ReferenceDate.Date,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0)
        });
        OnPropertyChanged(nameof(CanApply));
    }

    public IReadOnlyList<RecognizedScheduleDraft> GetResult ( )
    {
        return Drafts.Select(d => d.Clone()).ToList();
    }
}
