using IHLDota2Bot.AppHost;

// Tie every child process (Game Manager, Discord, Aspire DCP) to this AppHost's
// lifetime so closing/killing the AppHost — even non-gracefully — reaps the whole
// tree instead of orphaning servers + conhost.exe wrappers. Must run before any
// child is launched, i.e. before the host is built.
ProcessJobTree.KillChildrenOnExit();

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("Dota2inHouseLeagueSQL", port: 52001)
    .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("Dota2inHouseLeagueDB");

var dota2 = builder.AddProject<Projects.IHLDota2Bot>("Dota2inHouseLeague")
    .WithReference(db)
    .WaitFor(db);

var discord = builder.AddProject<Projects.IHLDota2Bot_Discord>("Dota2inHouseLeague-Discord");

dota2.WithReference(discord);
discord.WithReference(dota2);

builder.Build().Run();
