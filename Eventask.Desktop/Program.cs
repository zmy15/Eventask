using Avalonia;
using Eventask.App;
using Eventask.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Eventask.Desktop;

using App = App.App;

internal sealed class Program
{

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        App.Host = CreateHost(args);
        App.Host.Start();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        App.Host.StopAsync().GetAwaiter().GetResult();
        App.Host.Dispose();
    }

    private static IHost CreateHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        if (builder.Environment.IsDevelopment())
        {
            builder.ConfigureOpenTelemetry();
        }

        // Register platform-specific services
        builder.Services.AddSingleton<ITokenStorageService, FileTokenStorageService>();
        builder.Services.AddSingleton<ILocalStorageService, FileLocalStorageService>();

        builder.ConfigureApp();
        builder.AddAppServices();
        builder.AddAppViewModels();

        return builder.Build();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
