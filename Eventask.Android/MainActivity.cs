using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Eventask.App;
using Eventask.App.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventask.Android;

using App = App.App;

[Activity(
    Label = "@string/app_name",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CreateAppBuilder()
    {
        App.Host = CreateHost([]);

        return base.CreateAppBuilder();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    private IHost CreateHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>()
        {
            { "AppOptions:ApiBackendUrl", "https://api.eventask.app" }
        });

        builder.Logging.AddDebug();

        // Register platform-specific token storage service
        builder.Services.AddSingleton<ITokenStorageService>(new SharedPreferencesTokenStorageService(this));
        builder.Services.AddSingleton<ILocalStorageService>(new SharedPreferencesLocalStorageService(this));

        builder.ConfigureApp();
        builder.AddAppServices();
        builder.AddAppViewModels();

        return builder.Build();
    }
}
