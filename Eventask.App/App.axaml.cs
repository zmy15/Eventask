using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Eventask.App.Services;
using Eventask.App.ViewModels;
using Eventask.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Eventask.App;

public partial class App : Application
{
    public static IHost Host { get; set; } = null!;

    public override void Initialize ( )
    {
        AvaloniaXamlLoader.Load(this);

#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted ( )
    {
        try
        {
            if ( ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop )
            {
                var isAuthenticated = Task.Run(() =>
                {
                    var services = Host.Services;
                    DisableAvaloniaDataAnnotationValidation();

                    // Try to load persisted token for auto-login
                    var authService = services.GetRequiredService<IAuthService>();
                    return authService.TryLoadTokenAsync().GetAwaiter().GetResult();
                }).GetAwaiter().GetResult();

                desktop.MainWindow = new Window
                {
                    Title = "Eventask",
                    Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Eventask.App/Assets/logo.png"))),
                    Content = isAuthenticated
                        ? new MainView { DataContext = Host.Services.GetService<MainViewModel>() }
                        : new LoginView { DataContext = Host.Services.GetService<LoginViewModel>() },
                    RequestedThemeVariant = ThemeVariant.Light
                };
            }
            else if ( ApplicationLifetime is ISingleViewApplicationLifetime singleView )
            {
                var isAuthenticated = Task.Run(() =>
                {
                    var services = Host.Services;
                    DisableAvaloniaDataAnnotationValidation();

                    // Try to load persisted token for auto-login
                    var authService = services.GetRequiredService<IAuthService>();
                    return authService.TryLoadTokenAsync().GetAwaiter().GetResult();
                }).GetAwaiter().GetResult();

                singleView.MainView = isAuthenticated
                    ? new MainView { DataContext = Host.Services.GetService<MainViewModel>() }
                    : new LoginView { DataContext = Host.Services.GetService<LoginViewModel>() };
            }
            else
            {
                var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();

                builder.ConfigureApp();
                builder.AddAppServices();
                builder.AddAppViewModels();

                Host = builder.Build();
            }
        }
        catch ( Exception ex )
        {
            // Log the exception for debugging - async void exceptions cannot be caught by the caller
            System.Diagnostics.Debug.WriteLine($"Error during framework initialization: {ex}");
            throw;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation ( )
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach ( var plugin in dataValidationPluginsToRemove )
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
