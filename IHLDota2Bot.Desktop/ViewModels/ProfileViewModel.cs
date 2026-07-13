using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Desktop.Models;
using IHLDota2Bot.Desktop.Services;

namespace IHLDota2Bot.Desktop.ViewModels;

/// <summary>
/// The signed-in player's profile, loaded from the authenticated <c>/me/*</c> endpoints
/// (rating, rank/percentile, record, recent results). Falls back to sample data offline.
/// </summary>
public partial class ProfileViewModel : ViewModelBase
{
    private readonly ApiClient? _api;
    private readonly ulong _myId;

    [ObservableProperty]
    private string _username = "Player";

    [ObservableProperty]
    private string _initial = "P";

    [ObservableProperty]
    private int _rating;

    [ObservableProperty]
    private int _wins;

    [ObservableProperty]
    private int _losses;

    [ObservableProperty]
    private int _matchesPlayed;

    [ObservableProperty]
    private double _winRate;

    [ObservableProperty]
    private int _rank;

    [ObservableProperty]
    private int _totalPlayers;

    [ObservableProperty]
    private double _percentile;

    [ObservableProperty]
    private bool _hasRank;

    [ObservableProperty]
    private bool _isOffline;

    public ObservableCollection<MatchRow> RecentMatches { get; } = new();

    /// <summary>Design-time / fallback constructor.</summary>
    public ProfileViewModel() => SeedMock();

    /// <summary>DI constructor — loads live data for the signed-in player.</summary>
    public ProfileViewModel(ApiClient api, AppSession session)
    {
        _api = api;
        _myId = session.Me?.UserId ?? 0;
        _ = RefreshAsync();
    }

    public override async Task RefreshAsync()
    {
        try
        {
            var rank = await _api!.GetMyRankAsync();
            var stats = rank?.Stats ?? await _api.GetMyStatsAsync();

            if (stats is not null)
                ApplyStats(stats);

            if (rank is not null)
            {
                Rank = rank.Rank;
                TotalPlayers = rank.TotalPlayers;
                Percentile = rank.Percentile;
                HasRank = true;
            }
            else
            {
                HasRank = false;
            }

            var matches = await _api.GetMyMatchesAsync(10);
            RecentMatches.Clear();
            if (matches is not null)
            {
                foreach (var match in matches.Matches)
                    RecentMatches.Add(ToRow(match, _myId));
            }

            IsOffline = false;
        }
        catch
        {
            SeedMock();
            IsOffline = true;
        }
    }

    private void ApplyStats(PlayerStatsResponse stats)
    {
        Username = stats.Username;
        Initial = string.IsNullOrEmpty(stats.Username)
            ? "?"
            : stats.Username[..1].ToUpperInvariant();
        Rating = stats.Rating;
        Wins = stats.Wins;
        Losses = stats.Losses;
        MatchesPlayed = stats.MatchesPlayed;
        WinRate = stats.WinRate;
    }

    internal static MatchRow ToRow(MatchSummary match, ulong myId)
    {
        var mine = match.Players.FirstOrDefault(p => p.UserId == myId);
        var when = match.CompletedAt ?? match.CreatedAt;
        return new MatchRow
        {
            IsWin = mine?.Won ?? false,
            Mode = "5v5 Match",
            When = RelativeTime(when),
            RatingChange = string.Empty,
        };
    }

    internal static string RelativeTime(DateTime utc)
    {
        var delta = DateTime.UtcNow - utc.ToUniversalTime();
        if (delta.TotalMinutes < 1)
            return "just now";
        if (delta.TotalMinutes < 60)
            return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours < 24)
            return $"{(int)delta.TotalHours}h ago";
        if (delta.TotalDays < 7)
            return $"{(int)delta.TotalDays}d ago";
        return utc.ToLocalTime().ToString("MMM d");
    }

    private void SeedMock()
    {
        Username = "YourName";
        Initial = "Y";
        Rating = 1340;
        Wins = 42;
        Losses = 31;
        MatchesPlayed = 73;
        WinRate = 42d / 73d;
        Rank = 7;
        TotalPlayers = 128;
        Percentile = 5.4;
        HasRank = true;
        RecentMatches.Clear();
        RecentMatches.Add(
            new MatchRow
            {
                IsWin = true,
                Mode = "5v5 Captains Mode",
                When = "2h ago",
                RatingChange = "+24",
            }
        );
        RecentMatches.Add(
            new MatchRow
            {
                IsWin = true,
                Mode = "5v5 Captains Mode",
                When = "5h ago",
                RatingChange = "+21",
            }
        );
        RecentMatches.Add(
            new MatchRow
            {
                IsWin = false,
                Mode = "5v5 All Pick",
                When = "Yesterday",
                RatingChange = "-19",
            }
        );
        RecentMatches.Add(
            new MatchRow
            {
                IsWin = true,
                Mode = "5v5 Captains Mode",
                When = "Yesterday",
                RatingChange = "+23",
            }
        );
        RecentMatches.Add(
            new MatchRow
            {
                IsWin = false,
                Mode = "5v5 All Pick",
                When = "2 days ago",
                RatingChange = "-18",
            }
        );
    }
}
