using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Desktop.Models;
using IHLDota2Bot.Desktop.Services;

namespace IHLDota2Bot.Desktop.ViewModels;

/// <summary>
/// The hero "Play" screen: join the queue, watch the lobby fill, ready up. When an
/// <see cref="ApiClient"/> is available it polls <c>/queue/status</c> every 2s and maps
/// it onto the slot grid; offline it shows sample data flagged via <see cref="IsOffline"/>.
/// </summary>
public partial class PlayViewModel : ViewModelBase
{
    private readonly ApiClient? _api;
    private readonly ToastService? _toasts;
    private readonly AppSession? _session;
    private readonly DispatcherTimer? _timer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReadyUp))]
    [NotifyPropertyChangedFor(nameof(CanUnready))]
    private bool _inQueue;

    /// <summary>Server-confirmed: the player is marked ready in the current lobby.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReadyUp))]
    [NotifyPropertyChangedFor(nameof(CanUnready))]
    private bool _myReady;

    public bool CanReadyUp => InQueue && !MyReady;
    public bool CanUnready => InQueue && MyReady;

    [ObservableProperty]
    private int _playersReady;

    [ObservableProperty]
    private int _playersPerLobby = 10;

    [ObservableProperty]
    private bool _isOffline;

    /// <summary>A lobby is currently forming/locked (ready-check phase).</summary>
    [ObservableProperty]
    private bool _lobbyFormed;

    /// <summary>Everyone in the lobby is ready — match is good to go.</summary>
    [ObservableProperty]
    private bool _matchReady;

    /// <summary>The player has a current in-progress match (the match-found card is showing).</summary>
    [ObservableProperty]
    private bool _matchFound;

    [ObservableProperty]
    private string? _lobbyName;

    [ObservableProperty]
    private string? _lobbyPassword;

    /// <summary>"You're on RADIANT/DIRE", or null when the side is unknown.</summary>
    [ObservableProperty]
    private string? _myTeamLabel;

    public ObservableCollection<QueueSlot> Slots { get; } = new();
    public ObservableCollection<TeamSlot> RadiantPlayers { get; } = new();
    public ObservableCollection<TeamSlot> DirePlayers { get; } = new();

    /// <summary>Design-time / fallback constructor.</summary>
    public PlayViewModel() => SeedMock();

    /// <summary>DI constructor — polls live queue status.</summary>
    public PlayViewModel(ApiClient api, ToastService toasts, AppSession session)
    {
        _api = api;
        _toasts = toasts;
        _session = session;
        _ = RefreshAsync();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
    }

    [RelayCommand]
    private async Task ToggleQueue()
    {
        // Optimistic flip; reverted when the server refuses (e.g. banned) and
        // reconciled with server truth on every poll.
        var joining = !InQueue;
        InQueue = joining;
        if (_api is null)
            return;
        try
        {
            var result = joining ? await _api.JoinQueueAsync() : await _api.LeaveQueueAsync();
            if (result is { Success: false })
            {
                InQueue = !joining;
                _toasts?.Error(FallbackIfEmpty(result.Message, "The server refused the request."));
                return;
            }

            await RefreshAsync();
            _toasts?.Success(joining ? "Joined the queue" : "Left the queue");
        }
        catch
        {
            InQueue = !joining; // revert on failure
            _toasts?.Error("Couldn't update the queue — is the server up?");
        }
    }

    [RelayCommand]
    private async Task Ready()
    {
        if (_api is null)
            return;
        try
        {
            var result = await _api.ReadyAsync();
            if (result is { Success: false })
            {
                _toasts?.Error(FallbackIfEmpty(result.Message, "Couldn't ready up."));
                return;
            }

            await RefreshAsync();
            _toasts?.Success("You're ready — waiting for the others");
        }
        catch
        {
            _toasts?.Error("Couldn't ready up — is the server up?");
        }
    }

    [RelayCommand]
    private async Task Unready()
    {
        if (_api is null)
            return;
        try
        {
            var result = await _api.UnreadyAsync();
            if (result is { Success: false })
            {
                _toasts?.Error(FallbackIfEmpty(result.Message, "Couldn't undo ready."));
                return;
            }

            await RefreshAsync();
            _toasts?.Success("You're not ready anymore");
        }
        catch
        {
            _toasts?.Error("Couldn't undo ready — is the server up?");
        }
    }

    public override async Task RefreshAsync()
    {
        try
        {
            var status = await _api!.GetQueueStatusAsync();
            if (status is not null)
                ApplyStatus(status);

            // Server truth for *my* membership — survives app restarts and
            // reverts optimistic flips the server refused.
            if (await _api!.GetMyQueueAsync() is { } mine)
            {
                InQueue = mine.InQueue;
                MyReady = mine.IsReady;
            }

            // Fetched on the same poll: the match-found card is the client-side
            // fallback for a missed Steam invite (teams + lobby password).
            ApplyMatch(await _api!.GetMyMatchAsync());
            IsOffline = false;
        }
        catch
        {
            if (Slots.Count == 0)
                SeedMock();
            IsOffline = true;
        }
    }

    private static string FallbackIfEmpty(string? message, string fallback) =>
        string.IsNullOrWhiteSpace(message) ? fallback : message;

    private void ApplyStatus(QueueStatusResponse status)
    {
        PlayersPerLobby = status.PlayersPerLobby;
        PlayersReady = status.PlayersReady;
        LobbyFormed = !string.IsNullOrEmpty(status.CurrentLobbyId);
        MatchReady = status.PlayersPerLobby > 0 && status.PlayersReady >= status.PlayersPerLobby;

        Slots.Clear();
        foreach (var player in status.Players)
            Slots.Add(new QueueSlot { Name = player.Username, IsReady = player.IsReady });
        for (var i = status.Players.Count; i < status.PlayersPerLobby; i++)
            Slots.Add(new QueueSlot());
    }

    private void ApplyMatch(CurrentMatchResponse? current)
    {
        if (current is null)
        {
            MatchFound = false;
            LobbyName = null;
            LobbyPassword = null;
            MyTeamLabel = null;
            RadiantPlayers.Clear();
            DirePlayers.Clear();
            return;
        }

        var myUserId = _session?.Me?.UserId;

        RadiantPlayers.Clear();
        DirePlayers.Clear();
        foreach (var player in current.Match.Players)
        {
            var slot = new TeamSlot { Name = player.Username, IsMe = player.UserId == myUserId };
            (player.Team == MatchTeam.Radiant ? RadiantPlayers : DirePlayers).Add(slot);
        }

        MyTeamLabel = current.Match.Players.FirstOrDefault(p => p.UserId == myUserId)?.Team switch
        {
            MatchTeam.Radiant => "You're on RADIANT",
            MatchTeam.Dire => "You're on DIRE",
            _ => null,
        };
        LobbyName = current.LobbyName;
        LobbyPassword = current.LobbyPassword;
        MatchFound = true;
    }

    private void SeedMock()
    {
        PlayersPerLobby = 10;
        PlayersReady = 6;
        Slots.Clear();
        var filled = new (string Name, bool Ready)[]
        {
            ("Miracle-", true),
            ("Topson", true),
            ("Ceb", true),
            ("SumaiL", false),
            ("Arteezy", true),
            ("Puppey", true),
        };
        foreach (var (name, ready) in filled)
            Slots.Add(new QueueSlot { Name = name, IsReady = ready });
        for (var i = filled.Length; i < PlayersPerLobby; i++)
            Slots.Add(new QueueSlot());
    }
}
