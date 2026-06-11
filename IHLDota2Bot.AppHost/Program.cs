using IHLDota2Bot.AppHost;

// Tie every child process (Game Manager, Discord, Aspire DCP) to this AppHost's
// lifetime so closing/killing the AppHost — even non-gracefully — reaps the whole
// tree instead of orphaning servers + conhost.exe wrappers. Must run before any
// child is launched, i.e. before the host is built.
ProcessJobTree.KillChildrenOnExit();

// Belt-and-suspenders: if the parent (e.g. the `dotnet run` launcher) is killed
// while this process survives, exit too so the kill-on-close job reaps the rest.
ProcessJobTree.ExitWhenParentExits();

// Container runtime: Podman (no Docker Desktop licensing). Default to it unless
// the environment explicitly overrides, and start its engine before Aspire
// provisions containers so the AppHost doesn't require a manual `podman machine
// start` first.
var containerRuntime = Environment.GetEnvironmentVariable("DOTNET_ASPIRE_CONTAINER_RUNTIME");
if (string.IsNullOrEmpty(containerRuntime))
{
    containerRuntime = "podman";
    Environment.SetEnvironmentVariable("DOTNET_ASPIRE_CONTAINER_RUNTIME", containerRuntime);
}
if (string.Equals(containerRuntime, "podman", StringComparison.OrdinalIgnoreCase))
{
    PodmanBootstrap.EnsureRunning();
}

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("Dota2inHouseLeagueSQL", port: 52001)
    .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("Dota2inHouseLeagueDB");

var dota2 = builder.AddProject<Projects.IHLDota2Bot>("Dota2inHouseLeague-Backend")
    .WithReference(db)
    .WaitFor(db);

var discord = builder.AddProject<Projects.IHLDota2Bot_Discord>("Dota2inHouseLeague-Backend-Discord");

dota2.WithReference(discord);
discord.WithReference(dota2);

// Angular admin frontend. Runs `npm run start` (ng serve) as an AppHost-managed
// resource so it shows up in the Aspire dashboard, starts automatically with the
// AppHost, and is reaped along with everything else on shutdown. ng serve binds
// 4200 and reaches the backend through its own proxy.conf.json, so the endpoint
// is registered un-proxied on that fixed port. Waits for the Game Manager so the
// proxy target is up before the UI loads.
builder.AddNpmApp("Dota2inHouseLeague-FrontEnd", "../IHLDota2BotFrontEnd")
    .WithHttpEndpoint(port: 4200, targetPort: 4200, isProxied: false)
    .WithExternalHttpEndpoints()
    .WaitFor(dota2);

builder.Build().Run();
