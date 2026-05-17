var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("Dota2inHouseLeagueSQL")
    .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("Dota2inHouseLeagueDB");

builder.AddProject<Projects.IHLDota2Bot>("Dota2inHouseLeague");
    //.WithReference(db)
    //.WaitFor(db);

builder.AddProject<Projects.IHLDota2Bot_Discord>("Dota2inHouseLeague-Discord");

builder.Build().Run();
