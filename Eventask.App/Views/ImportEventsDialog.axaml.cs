using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Eventask.App.Views;

public partial class ImportEventsDialog : Window
{
    public ImportEventsDialog ( )
    {
        InitializeComponent();
    }

    private async void OnLoadFileClicked (object? sender, RoutedEventArgs e)
    {
        try
        {
            if ( StorageProvider is null )
            {
                JsonInputBox.Text = "// 当前平台不支持文件选择";
                return;
            }

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = "选择 JSON 文件",
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("JSON 文件") { Patterns = new[] { "*.json" } },
                    new FilePickerFileType("所有文件") { Patterns = new[] { "*.*" } }
                }
            });

            var file = files.FirstOrDefault();
            if ( file != null && File.Exists(file.Path.LocalPath) )
            {
                var content = await File.ReadAllTextAsync(file.Path.LocalPath);
                JsonInputBox.Text = content;
            }
        }
        catch ( Exception ex )
        {
            JsonInputBox.Text = $"// 读取文件失败: {ex.Message}";
        }
    }

    private void OnFillSampleClicked (object? sender, RoutedEventArgs e)
    {
        JsonInputBox.Text = """
{
  \"events\": [
    {
      \"name\": \"产品发布会\",
      \"description\": \"路线图分享\",
      \"location\": \"上海张江\",
      \"start\": \"2025-01-20T09:00:00+08:00\",
      \"end\": \"2025-01-20T11:30:00+08:00\",
      \"allDay\": false
    }
  ]
}
""";
    }

    private void OnCancelClicked (object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnImportClicked (object? sender, RoutedEventArgs e)
    {
        var content = JsonInputBox.Text?.Trim();
        if ( string.IsNullOrWhiteSpace(content) )
        {
            Close(null);
            return;
        }

        Close(content);
    }
}
