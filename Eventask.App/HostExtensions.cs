using Eventask.App.Models;
using Eventask.App.Services;
using Eventask.App.Services.Generated;
using Eventask.App.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Refit;

namespace Eventask.App;

public static class HostExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder ConfigureApp()
        {
            builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(nameof(AppOptions)));
            builder.Services.Configure<NlpOptions>(builder.Configuration.GetSection(nameof(NlpOptions)));
            return builder;
        }

        public IHostApplicationBuilder AddAppServices()
        {
            AppOptions options = new();
            builder.Configuration.GetSection(nameof(AppOptions)).Bind(options);

            // 注册认证服务
            builder.Services.AddSingleton<IAuthService, AuthService>();

            // 注册日历状态管理服务
            builder.Services.AddSingleton<ICalendarStateService, CalendarStateService>();

            // 注册导入服务
            builder.Services.AddSingleton<IEventImportService, EventImportService>();

            // 注册附件服务
            builder.Services.AddSingleton<IAttachmentService, AttachmentService>();

            // 注册自然语言解析服务（占位实现）
            builder.Services.AddSingleton<INaturalLanguageParser, NaturalLanguageParser>();

            // 注册 API 客户端 with Bearer token authentication
            builder.Services.AddRefitClient<IEventaskApi>(provider =>
            {
                var serializationOptions = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                    Converters =
                    {
                        new ObjectToInferredTypesConverter()
                    }
                };
                var authService = provider.GetRequiredService<IAuthService>();
                return new RefitSettings
                {
                    AuthorizationHeaderValueGetter = (_, _) =>
                    {
                        var token = authService.Token;
                        return Task.FromResult(token ?? "");
                    },
                    ContentSerializer = new SystemTextJsonContentSerializer(serializationOptions)
                };
            }).ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(options.ApiBackendUrl ?? throw new InvalidOperationException("API backend URL is not configured."));
            });

            // 注册导航服务
            builder.Services.AddSingleton<INavigationService, NavigationService>();

            return builder;
        }

        public IHostApplicationBuilder AddAppViewModels()
        {
            // 注册所有 ViewModels
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<RegisterViewModel>();
            // 保持为 Singleton - 在应用生命周期内只有一个实例
            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddTransient<CreateCalendarViewModel>();
            builder.Services.AddTransient<SelectCalendarViewModel>();
            builder.Services.AddTransient<DayDetailViewModel>();
            builder.Services.AddTransient<EditScheduleItemViewModel>();
            builder.Services.AddTransient<CalendarMembersViewModel>();
            builder.Services.AddTransient<NaturalLanguageDialogViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();

            return builder;
        }
    }
}
