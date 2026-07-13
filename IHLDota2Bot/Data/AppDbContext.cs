using IHLDota2Bot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IHLDota2Bot.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
    public DbSet<MatchEntity> Matches => Set<MatchEntity>();
    public DbSet<MatchPlayerEntity> MatchPlayers => Set<MatchPlayerEntity>();
    public DbSet<ChallengeEntity> Challenges => Set<ChallengeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PlayerEntity>(entity =>
        {
            entity.ToTable("Players");
            entity.HasKey(p => p.UserId);

            entity.Property(p => p.UserId).HasColumnType("decimal(20,0)").ValueGeneratedNever();

            entity.Property(p => p.Username).IsRequired().HasMaxLength(128);

            entity.Property(p => p.SteamId).IsRequired().HasMaxLength(32);

            entity.HasIndex(p => p.SteamId).IsUnique();

            entity.Property(p => p.Rating).HasDefaultValue(LeagueConstants.DefaultRating);
            entity.Property(p => p.Wins).HasDefaultValue(0);
            entity.Property(p => p.Losses).HasDefaultValue(0);
            entity.Property(p => p.IsBanned).HasDefaultValue(false);
            entity.Property(p => p.BanReason).HasMaxLength(256);
        });

        modelBuilder.Entity<MatchEntity>(entity =>
        {
            entity.ToTable("Matches");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.LobbyId).HasMaxLength(64);
            entity.Property(m => m.Status).HasConversion<int>();
            entity.Property(m => m.WinningTeam).HasConversion<int?>();
            entity.HasIndex(m => m.Status);
            entity.HasIndex(m => m.CompletedAt);
        });

        modelBuilder.Entity<MatchPlayerEntity>(entity =>
        {
            entity.ToTable("MatchPlayers");
            entity.HasKey(mp => new { mp.MatchId, mp.UserId });

            entity.Property(mp => mp.UserId).HasColumnType("decimal(20,0)").ValueGeneratedNever();

            entity.Property(mp => mp.Team).HasConversion<int>();

            entity
                .HasOne(mp => mp.Match)
                .WithMany(m => m.Players)
                .HasForeignKey(mp => mp.MatchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(mp => mp.Player)
                .WithMany()
                .HasForeignKey(mp => mp.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(mp => mp.UserId);
        });

        modelBuilder.Entity<ChallengeEntity>(entity =>
        {
            entity.ToTable("Challenges");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.ChallengerUserId).HasColumnType("decimal(20,0)");
            entity.Property(c => c.ChallengedUserId).HasColumnType("decimal(20,0)");
            entity.Property(c => c.Status).HasConversion<int>();

            entity.HasIndex(c => new { c.ChallengedUserId, c.Status });
            entity.HasIndex(c => new { c.ChallengerUserId, c.Status });
            entity.HasIndex(c => c.ExpiresAt);
        });
    }
}
