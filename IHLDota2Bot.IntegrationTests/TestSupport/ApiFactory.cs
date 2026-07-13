using IHLDota2Bot.Clients;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.Data;
using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace IHLDota2Bot.IntegrationTests.TestSupport;

/// <summary>
/// Boots the real Game Manager API in-process (TestServer) with two production
/// dependencies swapped for test doubles:
/// <list type="bullet">
///   <item>SQL Server → a private EF Core in-memory store (isolated per factory).</item>
///   <item>The Discord webhook HttpClient → <see cref="StubOkHandler"/>.</item>
/// </list>
/// Everything else is the real wiring: minimal-API endpoints, <c>Dota2Service</c>,
/// the EF repositories, <c>EloCalculator</c>, and the hosted services. Steam
/// credentials are force-cleared via <c>PostConfigure&lt;SteamOptions&gt;</c> so the
/// pool builds zero clients and never opens a real connection — regardless of what
/// the developer's user-secrets or environment contain (the TestServer runs in
/// Development and would otherwise load them).
///
/// <para><see cref="Dota2Service"/> is only an assembly marker for the entry point
/// (the app's top-level <c>Program</c> is internal); the factory never instantiates it.</para>
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Dota2Service>
{
    // Private root → each ApiFactory instance gets its own isolated database, so
    // tests running against separate factories never see each other's data.
    private readonly InMemoryDatabaseRoot dbRoot = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development so MapDefaultEndpoints exposes /health + /alive, and the
        // pipeline skips HTTPS redirection (TestServer speaks plain HTTP).
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Tests must be hermetic: strip every Steam account so SteamGcClientPool
            // builds no clients and the boot-time auto-connect has nothing to start.
            // PostConfigure runs after config binding, so this also neutralises real
            // credentials coming from the developer's user-secrets or env vars.
            services.PostConfigure<SteamOptions>(options =>
            {
                options.BotAccount = new SteamAccountCredentials();
                options.PoolAccounts = [];
            });

            // Drop the SQL Server DbContext factory wiring and replace it with a
            // shared in-memory store. Catches the factory, options, source and
            // (EF 9+) options-configuration descriptors closed over AppDbContext.
            var efDescriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions)
                    || (
                        d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericArguments().Contains(typeof(AppDbContext))
                    )
                )
                .ToList();
            foreach (var d in efDescriptors)
            {
                services.Remove(d);
            }

            services.AddDbContextFactory<AppDbContext>(o =>
                o.UseInMemoryDatabase("ihl-itests", dbRoot)
            );

            // Re-point the Discord webhook client at a benign local address with a
            // 200-stub primary handler (last registration wins for both).
            services
                .AddHttpClient(
                    DiscordClient.HttpClientName,
                    c => c.BaseAddress = new Uri("http://localhost/")
                )
                .ConfigurePrimaryHttpMessageHandler(() => new StubOkHandler());
        });
    }
}
