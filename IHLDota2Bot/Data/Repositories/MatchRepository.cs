using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IHLDota2Bot.Data.Repositories;

public class MatchRepository : IMatchRepository
{
    private readonly IDbContextFactory<AppDbContext> contextFactory;

    public MatchRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        this.contextFactory = contextFactory;
    }

    public async Task<MatchEntity> CreateMatchAsync(
        string? lobbyId,
        IEnumerable<(ulong UserId, MatchTeam Team)> players,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(players);

        var match = new MatchEntity
        {
            Id = Guid.NewGuid(),
            LobbyId = lobbyId,
            CreatedAt = DateTime.UtcNow,
            Status = MatchStatus.InProgress
        };

        foreach (var (userId, team) in players)
        {
            match.Players.Add(new MatchPlayerEntity
            {
                MatchId = match.Id,
                UserId = userId,
                Team = team,
                Won = false
            });
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Matches.Add(match);
        await context.SaveChangesAsync(cancellationToken);
        return match;
    }

    public async Task<MatchSummary?> GetCurrentMatchForPlayerAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var match = await context.Matches
            .AsNoTracking()
            .Include(m => m.Players)
            .Where(m => m.Status == MatchStatus.InProgress && m.Players.Any(p => p.UserId == userId))
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return match == null ? null : await ToSummaryAsync(context, match, cancellationToken);
    }

    public async Task<MatchSummary?> GetByIdAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var match = await context.Matches
            .AsNoTracking()
            .Include(m => m.Players)
            .FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken);

        return match == null ? null : await ToSummaryAsync(context, match, cancellationToken);
    }

    public async Task<MatchSummary?> GetByLobbyIdAsync(string lobbyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lobbyId)) return null;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var match = await context.Matches
            .AsNoTracking()
            .Include(m => m.Players)
            .Where(m => m.LobbyId == lobbyId)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return match == null ? null : await ToSummaryAsync(context, match, cancellationToken);
    }

    public async Task<RecentMatchesResponse> GetRecentMatchesForPlayerAsync(ulong userId, int top, CancellationToken cancellationToken = default)
    {
        if (top <= 0) top = 5;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var matches = await context.Matches
            .AsNoTracking()
            .Include(m => m.Players)
            .Where(m => m.Players.Any(p => p.UserId == userId) && m.Status == MatchStatus.Completed)
            .OrderByDescending(m => m.CompletedAt)
            .Take(top)
            .ToListAsync(cancellationToken);

        var summaries = new List<MatchSummary>(matches.Count);
        foreach (var match in matches)
        {
            summaries.Add(await ToSummaryAsync(context, match, cancellationToken));
        }

        return new RecentMatchesResponse
        {
            UserId = userId,
            Count = summaries.Count,
            Matches = summaries
        };
    }

    public async Task<ReportMatchResult> ReportMatchAsync(ulong reporterUserId, MatchTeam winningTeam, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var match = await context.Matches
            .Include(m => m.Players)
            .Where(m => m.Status == MatchStatus.InProgress && m.Players.Any(p => p.UserId == reporterUserId))
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (match == null)
        {
            return new ReportMatchResult { Success = false, Message = "You don't have an active match to report." };
        }

        var (radiantDelta, direDelta) = await ApplyResultAsync(context, match, winningTeam, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var summary = await ToSummaryAsync(context, match, cancellationToken);
        var message = $"Match {match.Id} reported. Winner: {winningTeam}. Δ Radiant {radiantDelta:+#;-#;0}, Dire {direDelta:+#;-#;0}.";
        return new ReportMatchResult { Success = true, Message = message, Match = summary };
    }

    public async Task<ReportMatchResult> CompleteMatchByLobbyAsync(string lobbyId, MatchTeam? winningTeam, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lobbyId);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var match = await context.Matches
            .Include(m => m.Players)
            .Where(m => m.LobbyId == lobbyId)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (match == null)
        {
            return new ReportMatchResult { Success = false, Message = $"No match found for lobby {lobbyId}." };
        }

        // Idempotent: the GC re-sends POSTGAME snapshots, and an admin/!report may
        // have already settled this match. Don't re-apply rating — just hand back
        // what we have.
        if (match.Status != MatchStatus.InProgress)
        {
            var existing = await ToSummaryAsync(context, match, cancellationToken);
            return new ReportMatchResult
            {
                Success = true,
                Message = $"Match {match.Id} already settled ({match.Status}).",
                Match = existing
            };
        }

        string message;
        if (winningTeam is { } winner)
        {
            var (radiantDelta, direDelta) = await ApplyResultAsync(context, match, winner, cancellationToken);
            message = $"Match {match.Id} completed. Winner: {winner}. Δ Radiant {radiantDelta:+#;-#;0}, Dire {direDelta:+#;-#;0}.";
        }
        else
        {
            // The GC didn't score the game (abandon, crash, never started). Close
            // it out without touching anyone's rating.
            match.Status = MatchStatus.Cancelled;
            match.CompletedAt = DateTime.UtcNow;
            message = $"Match {match.Id} not scored by the Dota GC; cancelled with no rating change.";
        }

        await context.SaveChangesAsync(cancellationToken);

        var summary = await ToSummaryAsync(context, match, cancellationToken);
        return new ReportMatchResult { Success = true, Message = message, Match = summary };
    }

    /// <summary>
    /// Marks <paramref name="match"/> Completed for <paramref name="winningTeam"/>,
    /// applies W/L and an ELO delta to every participant, and returns the
    /// (radiant, dire) rating deltas. Mutates tracked entities only — the caller
    /// owns SaveChanges.
    /// </summary>
    private static async Task<(int RadiantDelta, int DireDelta)> ApplyResultAsync(
        AppDbContext context,
        MatchEntity match,
        MatchTeam winningTeam,
        CancellationToken cancellationToken)
    {
        match.Status = MatchStatus.Completed;
        match.CompletedAt = DateTime.UtcNow;
        match.WinningTeam = winningTeam;

        var userIds = match.Players.Select(p => p.UserId).ToList();
        var players = await context.Players.Where(p => userIds.Contains(p.UserId)).ToListAsync(cancellationToken);
        var playersById = players.ToDictionary(p => p.UserId);

        var radiantUserIds = match.Players.Where(p => p.Team == MatchTeam.Radiant).Select(p => p.UserId).ToHashSet();
        var radiantRatings = players.Where(p => radiantUserIds.Contains(p.UserId)).Select(p => p.Rating).ToList();
        var direRatings = players.Where(p => !radiantUserIds.Contains(p.UserId)).Select(p => p.Rating).ToList();

        var (radiantDelta, direDelta) = EloCalculator.ComputeDeltas(radiantRatings, direRatings, winningTeam);

        foreach (var mp in match.Players)
        {
            if (!playersById.TryGetValue(mp.UserId, out var player)) continue;

            mp.Won = mp.Team == winningTeam;
            if (mp.Won) player.Wins++;
            else player.Losses++;

            player.Rating += mp.Team == MatchTeam.Radiant ? radiantDelta : direDelta;
        }

        return (radiantDelta, direDelta);
    }

    public async Task<List<MatchEntity>> GetStaleInProgressMatchesAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Matches
            .AsNoTracking()
            .Where(m => m.Status == MatchStatus.InProgress && m.CreatedAt < olderThanUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CancelMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var match = await context.Matches.FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken);
        if (match == null) return false;
        if (match.Status != MatchStatus.InProgress) return false; // already settled — don't overwrite

        match.Status = MatchStatus.Cancelled;
        match.CompletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static async Task<MatchSummary> ToSummaryAsync(AppDbContext context, MatchEntity match, CancellationToken cancellationToken)
    {
        var userIds = match.Players.Select(p => p.UserId).ToList();
        var usernames = await context.Players
            .AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.Username, cancellationToken);

        return new MatchSummary
        {
            Id = match.Id,
            LobbyId = match.LobbyId,
            CreatedAt = match.CreatedAt,
            CompletedAt = match.CompletedAt,
            Status = match.Status,
            WinningTeam = match.WinningTeam,
            Players = match.Players
                .OrderBy(p => p.Team)
                .Select(p => new MatchPlayerSummary
                {
                    UserId = p.UserId,
                    Username = usernames.GetValueOrDefault(p.UserId, p.UserId.ToString()),
                    Team = p.Team,
                    Won = p.Won
                })
                .ToList()
        };
    }
}
