using CommunityToolkit.Aspire.Hosting.Ollama;

var builder = DistributedApplication.CreateBuilder(args);

var qdrant = builder.AddQdrant("qdrant")
    .WithLifetime(ContainerLifetime.Persistent);

var serverProject = builder.AddProject<Projects.QdrantSkillsMCP_Infrastructure>("server")
    .WithReference(qdrant)
    .WaitFor(qdrant);

// Conditionally add Ollama container when embedding provider is Ollama
var useOllama = builder.Configuration["QdrantSkills:EmbeddingProvider"]
    ?.Equals("Ollama", StringComparison.OrdinalIgnoreCase) == true;

if (useOllama)
{
    var ollama = builder.AddOllama("ollama")
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent);

    var embeddingModel = ollama.AddModel("embedding", "all-minilm:l6-v2");

    serverProject
        .WithReference(ollama)
        .WaitFor(embeddingModel);
}

builder.Build().Run();
