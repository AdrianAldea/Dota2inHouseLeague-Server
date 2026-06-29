using System;
using System.Collections.ObjectModel;
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
    private readonly DispatcherTimer? _timer;

    [ObservableProperty]
    private bool _inQueue;

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

    public ObservableCollection<QueueSlot> Slots { get; } = new();

    /// <summary>Design-time / fallback constructor.</summary>
    public PlayViewModel() => SeedMock();

    /// <summary>DI constructor — polls live queue status.</summary>
    public PlayViewModel(ApiClient api, ToastService toasts)
    {
        _api = api;
        _toasts = toasts;
        _ = RefreshAsync();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
    }

    [RelayCommand]
    private async Task ToggleQueue()
    {
        // Optimistic flip; the next poll reconciles with server truth.
        var joining = !InQueue;
        InQueue = joining;
        if (_api is null)
            return;
        try
        {
            if (joining)
                await _api.JoinQueueAsync();
            else
                await _api.LeaveQueueAsync();
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
            await _api.ReadyAsync();
            await RefreshAsync();
            _toasts?.Success("You're ready — waiting for the others");
        }
        catch
        {
            _toasts?.Error("Couldn't ready up — is the server up?");
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            var status = await _api!.GetQueueStatusAsync();
            if (status is not null)
                ApplyStatus(status);
            IsOffline = false;
        }
        catch
        {
            if (Slots.Count == 0)
                SeedMock();
            IsOffline = true;
        }
    }

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

    private void SeedMock()
    {
        PlayersPerLobby = 10;
        PlayersReady = 6;
        Slots.Clear();
        var filled = new (string Name, bool Ready)[]
        {
            ("Miracle-", true), ("Topson", true), ("Ceb", true),
            ("SumaiL", false), ("Arteezy", true), ("Puppey", true),
        };
        foreach (var (name, ready) in filled)
            Slots.Add(new QueueSlot { Name = name, IsReady = ready });
        for (var i = filled.Length; i < PlayersPerLobby; i++)
            Slots.Add(new QueueSlot());
    }
}
