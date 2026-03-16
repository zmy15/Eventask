using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace Eventask.App.ViewModels
{
	public partial class SelectReminderDialog : Window
	{
		public SelectReminderDialog()
		{
			InitializeComponent();
		}

		private void OnCancel(object? sender, RoutedEventArgs e)
		{
			Close(null);
		}

		private void OnConfirm(object? sender, RoutedEventArgs e)
		{
			var listBox = this.FindControl<ListBox>("ReminderList");
			if (listBox?.SelectedItem is ListBoxItem item && item.Tag is string tagStr)
			{
				if (int.TryParse(tagStr, out var offsetMinutes))
				{
					Close(offsetMinutes);
					return;
				}
			}
			Close(null);
		}
	}
}