using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IHLDota2Bot.Data.Repositories;

public class MatchRepository : IMatchRepository
{
    private const int EloKFactor = 32;

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

        match.Status = MatchStatus.Completed;
        match.CompletedAt = DateTime.UtcNow;
        match.WinningTeam = winningTeam;

        // Apply ELO + W/L to participants
        var userIds = match.Players.Select(p => p.UserId).ToList();
        var players = await context.Players.Where(p => userIds.Contains(p.UserId)).ToListAsync(cancellationToken);
        var playersById = players.ToDictionary(p => p.UserId);

        var radiantUserIds = match.Players.Where(p => p.Team == MatchTeam.Radiant).Select(p => p.UserId).ToHashSet();
        var radiantRatings = players.Where(p => radiantUserIds.Contains(p.UserId)).Select(p => p.Rating).ToList();
        var direRatings = players.Where(p => !radiantUserIds.Contains(p.UserId)).Select(p => p.Rating).ToList();

        var radiantDelta = 0;
        var direDelta = 0;
        if (radiantRatings.Count > 0 && direRatings.Count > 0)
        {
            var avgRadiant = radiantRatings.Average();
            var avgDire = direRatings.Average();
            var expectedRadiant = 1.0 / (1.0 + Math.Pow(10, (avgDire - avgRadiant) / 400.0));
            var actualRadiant = winningTeam == MatchTeam.Radiant ? 1.0 : 0.0;
            radiantDelta = (int)Math.Round(EloKFactor * (actualRadiant - expectedRadiant));
            direDelta = -radiantDelta;
        }

        foreach (var mp in match.Players)
        {
            if (!playersById.TryGetValue(mp.UserId, out var player)) continue;

            mp.Won = mp.Team == winningTeam;
            if (mp.Won) player.Wins++;
            else player.Losses++;

            player.Rating += mp.Team == MatchTeam.Radiant ? radiantDelta : direDelta;
        }

        await context.SaveChangesAsync(cancellationToken);

        var summary = await ToSummaryAsync(context, match, cancellationToken);
        var message = $"Match {match.Id} reported. Winner: {winningTeam}. Δ Radiant {radiantDelta:+#;-#;0}, Dire {direDelta:+#;-#;0}.";
        return new ReportMatchResult { Success = true, Message = message, Match = summary };
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
