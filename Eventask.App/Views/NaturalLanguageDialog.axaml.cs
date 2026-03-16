using Avalonia.Controls;
using Avalonia.Styling;
using Eventask.App.ViewModels;

namespace Eventask.App.Views;

public partial class NaturalLanguageDialog : Window
{
    public NaturalLanguageDialog()
    {
        InitializeComponent();
        this.RequestedThemeVariant = ThemeVariant.Light;
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnApply(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is NaturalLanguageDialogViewModel vm)
        {
            Close(vm.GetResult());
        }
        else
        {
            Close(null);
        }
    }
}
