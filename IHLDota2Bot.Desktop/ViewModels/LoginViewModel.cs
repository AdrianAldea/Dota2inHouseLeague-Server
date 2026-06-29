using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHLDota2Bot.Desktop.Services;

namespace IHLDota2Bot.Desktop.ViewModels;

/// <summary>
/// "Sign in through Steam" screen. On success the <see cref="AppSession"/> changes and
/// <see cref="MainWindowViewModel"/> navigates away automatically.
/// </summary>
public partial class LoginViewModel : ViewModelBase
{
    private readonly AuthService? _auth;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _error;

    /// <summary>Design-time / fallback constructor.</summary>
    public LoginViewModel() { }

    public LoginViewModel(AuthService auth) => _auth = auth;

    [RelayCommand]
    private async Task SignIn()
    {
        if (_auth is null)
            return;

        IsBusy = true;
        Error = null;
        try
        {
            var ok = await _auth.SignInWithSteamAsync();
            if (!ok)
                Error = "Sign-in was cancelled or didn't complete. Please try again.";
        }
        catch (Exception)
        {
            Error = "Couldn't reach the league server. Is it running?";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
