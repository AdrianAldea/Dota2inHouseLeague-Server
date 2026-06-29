using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHLDota2Bot.Desktop.Services;

namespace IHLDota2Bot.Desktop.ViewModels;

/// <summary>
/// Shown when a Steam account signs in but has no league account yet: pick a display
/// name to register. On success the session flips to registered and the shell appears.
/// </summary>
public partial class RegistrationViewModel : ViewModelBase
{
    private readonly AuthService? _auth;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _error;

    public string SteamId { get; }

    /// <summary>Design-time / fallback constructor.</summary>
    public RegistrationViewModel() => SteamId = "—";

    public RegistrationViewModel(AuthService auth, AppSession session)
    {
        _auth = auth;
        SteamId = session.Me?.SteamId ?? "—";
    }

    [RelayCommand]
    private async Task Submit()
    {
        if (_auth is null)
            return;

        if (string.IsNullOrWhiteSpace(Username))
        {
            Error = "Please pick a display name.";
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            var ok = await _auth.RegisterAsync(Username.Trim());
            if (!ok)
                Error = "Registration failed. Please try again.";
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
