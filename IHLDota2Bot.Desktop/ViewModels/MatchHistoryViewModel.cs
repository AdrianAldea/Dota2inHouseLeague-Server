using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHLDota2Bot.Desktop.Models;
using IHLDota2Bot.Desktop.Services;

namespace IHLDota2Bot.Desktop.ViewModels;

/// <summary>
/// Full match history for the signed-in player, from <c>/me/matches</c>. Clicking a row
/// opens an in-place detail overlay with both teams. Sample data offline.
/// </summary>
public partial class MatchHistoryViewModel : ViewModelBase
{
    private readonly ApiClient? _api;
    private readonly ulong _myId;

    public ObservableCollection<MatchListItem> Matches { get; } = new();

    [ObservableProperty]
    private bool _isOffline;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private MatchListItem? _selectedMatch;

    /// <summary>True while a match's detail overlay is open.</summary>
    public bool IsDetailOpen => SelectedMatch is not null;

    /// <summary>Design-time / fallback constructor.</summary>
    public MatchHistoryViewModel() => SeedMock();

    /// <summary>DI constructor — loads live data.</summary>
    public MatchHistoryViewModel(ApiClient api, AppSession session)
    {
        _api = api;
        _myId = session.Me?.UserId ?? 0;
        _ = LoadAsync();
    }

    partial void OnSelectedMatchChanged(MatchListItem? value) => OnPropertyChanged(nameof(IsDetailOpen));

    [RelayCommand]
    private void SelectMatch(MatchListItem? item) => SelectedMatch = item;

    [RelayCommand]
    private void CloseDetail() => SelectedMatch = null;

    private async Task LoadAsync()
    {
        try
        {
            var response = await _api!.GetMyMatchesAsync(25);
            Matches.Clear();
            if (response is not null)
            {
                foreach (var match in response.Matches)
                {
                    var when = ProfileViewModel.RelativeTime(match.CompletedAt ?? match.CreatedAt);
                    Matches.Add(MatchListItem.FromSummary(match, _myId, when));
                }
            }
            IsEmpty = Matches.Count == 0;
            IsOffline = false;
        }
        catch
        {
            SeedMock();
            IsOffline = true;
        }
    }

    private void SeedMock()
    {
        Matches.Clear();
        Matches.Add(Mock(true, "2h ago"));
        Matches.Add(Mock(true, "5h ago"));
        Matches.Add(Mock(false, "Yesterday"));
        Matches.Add(Mock(true, "Yesterday"));
        Matches.Add(Mock(false, "2 days ago"));
        Matches.Add(Mock(true, "3 days ago"));
        IsEmpty = false;
    }

    private static MatchListItem Mock(bool win, string when)
    {
        // The player ("YourName") is on Radiant, so Radiant won iff this was a win.
        var radiant = new List<MatchPlayerLine>
        {
            new() { Name = "YourName", Won = win, IsMe = true },
            new() { Name = "Miracle-", Won = win },
            new() { Name = "Topson",   Won = win },
            new() { Name = "Ceb",      Won = win },
            new() { Name = "SumaiL",   Won = win },
        };
        var dire = new List<MatchPlayerLine>
        {
            new() { Name = "Puppey",   Won = !win },
            new() { Name = "Nisha",    Won = !win },
            new() { Name = "Ana",      Won = !win },
            new() { Name = "33",       Won = !win },
            new() { Name = "Arteezy",  Won = !win },
        };
        return new MatchListItem
        {
            IsWin = win, Mode = "5v5 Captains Mode", When = when,
            RadiantWon = win, Radiant = radiant, Dire = dire,
        };
    }
}
