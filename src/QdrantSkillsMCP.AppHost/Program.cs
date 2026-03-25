var builder = DistributedApplication.CreateBuilder(args);

var qdrant = builder.AddQdrant("qdrant")
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.QdrantSkillsMCP_Infrastructure>("server")
    .WithReference(qdrant)
    .WaitFor(qdrant);

builder.Build().Run();
