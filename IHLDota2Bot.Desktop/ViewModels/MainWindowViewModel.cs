using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using IHLDota2Bot.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IHLDota2Bot.Desktop.ViewModels;

/// <summary>
/// Root view-model behind the window. Owns the auth gate: it auto-logins on startup and
/// then swaps <see cref="CurrentView"/> between Login, Registration, and the Shell as the
/// <see cref="AppSession"/> changes. Pages are resolved lazily from the DI container.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly AuthService? _auth;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Toast notifications, rendered by the window's toast host.</summary>
    public ToastService Toasts { get; }

    /// <summary>Design-time / fallback constructor.</summary>
    public MainWindowViewModel()
    {
        Toasts = new ToastService();
        CurrentView = new LoginViewModel();
    }

    public MainWindowViewModel(AuthService auth, ToastService toasts)
    {
        _auth = auth;
        Toasts = toasts;
        _auth.Session.Changed += OnSessionChanged;
        _ = StartAsync();
    }

    private async Task StartAsync()
    {
        IsLoading = true;
        await _auth!.TryAutoLoginAsync();
        Navigate();
        IsLoading = false;
    }

    private void OnSessionChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(Navigate);

    private void Navigate()
    {
        if (_auth is null)
            return;

        if (!_auth.Session.IsAuthenticated)
            CurrentView = Resolve<LoginViewModel>();
        else if (!_auth.Session.IsRegistered)
            CurrentView = Resolve<RegistrationViewModel>();
        else
            CurrentView = Resolve<ShellViewModel>();
    }

    private static T Resolve<T>()
        where T : notnull => App.Services.GetRequiredService<T>();

    public void Dispose()
    {
        if (_auth is not null)
            _auth.Session.Changed -= OnSessionChanged;
    }
}
