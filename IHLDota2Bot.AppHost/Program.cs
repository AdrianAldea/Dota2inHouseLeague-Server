using IHLDota2Bot.AppHost;

// Tie every child process (Game Manager, Discord, Aspire DCP) to this AppHost's
// lifetime so closing/killing the AppHost — even non-gracefully — reaps the whole
// tree instead of orphaning servers + conhost.exe wrappers. Must run before any
// child is launched, i.e. before the host is built.
ProcessJobTree.KillChildrenOnExit();

// Belt-and-suspenders: if the parent (e.g. the `dotnet run` launcher) is killed
// while this process survives, exit too so the kill-on-close job reaps the rest.
ProcessJobTree.ExitWhenParentExits();

// Container runtime: Docker (Aspire's default). Make sure Docker Desktop's engine
// is up before Aspire provisions containers so the AppHost doesn't require it to
// be started by hand.
DockerBootstrap.EnsureRunning();

var builder = DistributedApplication.CreateBuilder(args);

// Pin the SA password to an explicit parameter instead of letting Aspire generate a
// random one. A generated password is only persisted in user secrets, so it silently
// regenerates whenever those secrets are missing (fresh clone, cleared secrets, another
// machine) — which both changes the SSMS connection string AND breaks login against the
// existing data volume, whose master DB still holds the original password. Pinning it
// makes the connection string (Server=127.0.0.1,52001; User ID=sa) identical every run.
// The value lives in user secrets under "Parameters:sql-password" and must match the
// password baked into the existing volume's master DB.
var sqlPassword = builder.AddParameter("sql-password", secret: true);

// A named data volume keeps the SQL data outside the container's writable layer, so
// the database (registrations, matches, ratings) survives container recreation —
// Docker Desktop restarts, prunes, or Aspire reprovisioning. Without it, data lived
// only inside the container and was wiped whenever the container was recreated.
var sql = builder.AddSqlServer("Dota2inHouseLeagueSQL", password: sqlPassword, port: 52001)
    .WithDataVolume("dota2ihl-sql-data")
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

// Avalonia desktop player client. Registered as an AppHost-managed resource (so it's
// reaped on shutdown) but with explicit start: it shows in the dashboard and only
// launches when you click Start, rather than popping a GUI window on every run. It
// reads its backend URL from IHL_API_BASE, which we point at the Game Manager's
// (dynamically assigned) endpoint instead of the appsettings.json default. Excluded
// from the manifest since a GUI client isn't a deployable service. Waits for the
// backend so auth calls land.
builder.AddProject<Projects.IHLDota2Bot_Desktop>("Dota2inHouseLeague-Desktop")
    .WithEnvironment("IHL_API_BASE", dota2.GetEndpoint("http"))
    .WaitFor(dota2)
    .WithExplicitStart()
    .ExcludeFromManifest();

builder.Build().Run();
