using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using IHLDota2Bot.Desktop.Services;
using IHLDota2Bot.Desktop.ViewModels;
using IHLDota2Bot.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace IHLDota2Bot.Desktop;

public partial class App : Application
{
    /// <summary>Resolved service container — set once at startup.</summary>
    public static IServiceProvider Services { get; private set; } = default!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Services = BuildServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider BuildServices()
    {
        var settings = AppSettings.Load();
        var services = new ServiceCollection();

        services.AddSingleton(settings);

        // Auth/session services
        services.AddSingleton<AppSession>();
        services.AddSingleton<TokenStore>();
        services.AddSingleton<SteamAuthService>();
        services.AddSingleton<AuthService>();
        services.AddSingleton<ToastService>();
        services.AddTransient<AuthHeaderHandler>();

        // Typed client rooted at {ApiBaseUrl}/api/; the handler attaches the Bearer token.
        services
            .AddHttpClient<ApiClient>(client =>
            {
                client.BaseAddress = new Uri($"{settings.ApiBaseUrl.TrimEnd('/')}/api/");
                client.Timeout = TimeSpan.FromSeconds(8);
            })
            .AddHttpMessageHandler<AuthHeaderHandler>();

        // View-models
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegistrationViewModel>();
        services.AddTransient<PlayViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<LeaderboardViewModel>();
        services.AddTransient<MatchHistoryViewModel>();

        return services.BuildServiceProvider();
    }
}
