using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IHLDota2Bot.Data;

/// <summary>
/// Lets the EF Core CLI (<c>dotnet ef migrations …</c>) build an
/// <see cref="AppDbContext"/> at design time without booting the full app or
/// reaching a live database. The connection string here is a placeholder used
/// only to pick the SQL Server provider so the migration scaffolder knows the
/// target dialect; it is never opened. At runtime the real connection string is
/// supplied by Aspire via <c>AddGameManager</c>.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb);Database=DesignTime;Trusted_Connection=True;")
            .Options;

        return new AppDbContext(options);
    }
}
