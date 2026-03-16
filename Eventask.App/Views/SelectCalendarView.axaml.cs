using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Eventask.App.Models;
using Eventask.App.ViewModels;

namespace Eventask.App.Views;

public partial class SelectCalendarView : UserControl
{
    public SelectCalendarView()
    {
        InitializeComponent();
    }

    private void OnCalendarItemTapped(object? sender, TappedEventArgs e)
    {
        // 获取被点击的 Border
        if (sender is Border border && border.DataContext is CalendarItemModel calendar)
        {
            // 获取 ViewModel 并执行选择命令
            if (DataContext is SelectCalendarViewModel viewModel)
            {
                viewModel.SelectCalendarCommand.Execute(calendar);
            }
        }

        // 标记事件已处理
        e.Handled = true;
    }

    private void OnDeleteButtonClick(object? sender, RoutedEventArgs e)
    {
        // 阻止事件冒泡到父容器
        e.Handled = true;
    }
}