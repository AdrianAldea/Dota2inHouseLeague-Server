var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("Dota2inHouseLeagueSQL")
    .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("Dota2inHouseLeagueDB");

var dota2 = builder.AddProject<Projects.IHLDota2Bot>("Dota2inHouseLeague")
    .WithReference(db)
    .WaitFor(db);

var discord = builder.AddProject<Projects.IHLDota2Bot_Discord>("Dota2inHouseLeague-Discord");

dota2.WithReference(discord);
discord.WithReference(dota2);

builder.Build().Run();
