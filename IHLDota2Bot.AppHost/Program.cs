var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.IHLDota2Bot>("dota2bot");

builder.Build().Run();
