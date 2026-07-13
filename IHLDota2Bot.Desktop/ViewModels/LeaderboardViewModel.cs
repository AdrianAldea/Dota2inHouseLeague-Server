using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Desktop.Services;

namespace IHLDota2Bot.Desktop.ViewModels;

/// <summary>
/// League ladder. Loads the real <c>/leaderboard</c> when an <see cref="ApiClient"/>
/// is available, and falls back to sample data (flagged via <see cref="IsOffline"/>)
/// when the backend can't be reached, so the screen always looks complete.
/// Reuses the backend <see cref="LeaderboardEntry"/> wire shape directly.
/// </summary>
public partial class LeaderboardViewModel : ViewModelBase
{
    private readonly ApiClient? _api;

    public ObservableCollection<LeaderboardEntry> Entries { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isOffline;

    /// <summary>Design-time / fallback constructor.</summary>
    public LeaderboardViewModel() => SeedMock();

    /// <summary>DI constructor — loads live data.</summary>
    public LeaderboardViewModel(ApiClient api)
    {
        _api = api;
        _ = RefreshAsync();
    }

    public override async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var response = await _api!.GetLeaderboardAsync(top: 25);
            Entries.Clear();
            if (response is { Entries.Count: > 0 })
            {
                foreach (var entry in response.Entries)
                    Entries.Add(entry);
            }
            IsOffline = false;
        }
        catch
        {
            SeedMock();
            IsOffline = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SeedMock()
    {
        Entries.Clear();
        var seed = new (string Name, int Rating, int W, int L)[]
        {
            ("Miracle-", 1612, 88, 40),
            ("Topson", 1574, 81, 44),
            ("SumaiL", 1551, 77, 41),
            ("Arteezy", 1530, 73, 45),
            ("Ceb", 1498, 69, 47),
            ("Puppey", 1471, 64, 48),
            ("YourName", 1340, 42, 31),
            ("Nisha", 1322, 51, 49),
            ("Ana", 1300, 33, 30),
            ("33", 1284, 40, 44),
        };
        for (var i = 0; i < seed.Length; i++)
        {
            var (name, rating, w, l) = seed[i];
            Entries.Add(
                new LeaderboardEntry
                {
                    Rank = i + 1,
                    UserId = (ulong)(1000 + i),
                    Username = name,
                    Rating = rating,
                    Wins = w,
                    Losses = l,
                }
            );
        }
    }
}
