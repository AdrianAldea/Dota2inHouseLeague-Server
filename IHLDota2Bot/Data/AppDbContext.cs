using IHLDota2Bot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IHLDota2Bot.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PlayerEntity>(entity =>
        {
            entity.ToTable("Players");
            entity.HasKey(p => p.UserId);

            entity.Property(p => p.UserId)
                .HasColumnType("decimal(20,0)")
                .ValueGeneratedNever();

            entity.Property(p => p.Username)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(p => p.SteamId)
                .IsRequired()
                .HasMaxLength(32);

            entity.HasIndex(p => p.SteamId).IsUnique();
        });
    }
}
