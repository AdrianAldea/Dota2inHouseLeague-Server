using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHLDota2Bot.Desktop.Models;
using IHLDota2Bot.Desktop.Services;

namespace IHLDota2Bot.Desktop.ViewModels;

/// <summary>
/// The signed-in shell: sidebar nav + content area + the player chip/logout. Shown by
/// <see cref="MainWindowViewModel"/> once the session is authenticated and registered.
/// </summary>
public partial class ShellViewModel : ViewModelBase
{
    private readonly AuthService? _auth;

    public ObservableCollection<NavItem> NavItems { get; }

    [ObservableProperty]
    private NavItem? _selectedNav;

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    public string PlayerName { get; }
    public string PlayerRating { get; }
    public int RatingValue { get; }
    public string PlayerInitial { get; }

    public ShellViewModel(
        PlayViewModel play,
        ProfileViewModel profile,
        LeaderboardViewModel leaderboard,
        MatchHistoryViewModel matches,
        AppSession session,
        AuthService auth)
    {
        _auth = auth;

        var name = session.Me?.Profile?.Username ?? "Player";
        PlayerName = name;
        RatingValue = session.Me?.Profile?.Rating ?? 0;
        PlayerRating = session.Me?.Profile?.Rating.ToString() ?? "—";
        PlayerInitial = string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();

        NavItems = new ObservableCollection<NavItem>
        {
            new() { Title = "Play",        Icon = Geo("IconPlay"),        Page = play },
            new() { Title = "Profile",     Icon = Geo("IconProfile"),     Page = profile },
            new() { Title = "Leaderboard", Icon = Geo("IconLeaderboard"), Page = leaderboard },
            new() { Title = "Matches",     Icon = Geo("IconMatches"),     Page = matches },
        };
        SelectedNav = NavItems[0];
    }

    /// <summary>Design-time / fallback constructor (mock pages, no live session).</summary>
    public ShellViewModel()
        : this(new PlayViewModel(), new ProfileViewModel(), new LeaderboardViewModel(),
               new MatchHistoryViewModel(), new AppSession(), null!)
    {
    }

    partial void OnSelectedNavChanged(NavItem? value)
    {
        if (value is not null)
            CurrentPage = value.Page;
    }

    [RelayCommand]
    private void Logout() => _auth?.Logout();

    private static Geometry Geo(string key)
    {
        if (Application.Current is { } app &&
            app.TryGetResource(key, ThemeVariant.Default, out var value) &&
            value is Geometry geometry)
        {
            return geometry;
        }
        return new PathGeometry();
    }
}
