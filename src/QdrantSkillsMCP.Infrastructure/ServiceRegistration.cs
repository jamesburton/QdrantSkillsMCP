using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using Qdrant.Client;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Infrastructure.Configuration;
using QdrantSkillsMCP.Infrastructure.Embedding;
using QdrantSkillsMCP.Infrastructure.Qdrant;
using QdrantSkillsMCP.Infrastructure.Session;
using QdrantSkillsMCP.Infrastructure.Yaml;

namespace QdrantSkillsMCP.Infrastructure;

/// <summary>
/// DI extension methods for registering all infrastructure services.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Registers all QdrantSkills infrastructure services in the DI container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQdrantSkillsInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        // Bind configuration
        services.Configure<QdrantSkillsOptions>(config.GetSection(QdrantSkillsOptions.SectionName));

        // QdrantClient -- singleton, created from options
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantSkillsOptions>>().Value;
            return new QdrantClient(
                options.QdrantHost,
                options.QdrantGrpcPort,
                apiKey: options.QdrantApiKey);
        });

        // CollectionInitializer -- singleton, lazy collection creation
        services.AddSingleton<CollectionInitializer>();

        // SkillParser -- singleton, stateless
        services.AddSingleton<SkillParser>();

        // ISkillRepository -> QdrantSkillRepository
        services.AddSingleton<ISkillRepository, QdrantSkillRepository>();

        // IEmbeddingGenerator<string, Embedding<float>> -- OpenAI provider
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantSkillsOptions>>().Value;

            // API key from config or OPENAI_API_KEY environment variable
            var apiKey = options.OpenAiApiKey
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? throw new InvalidOperationException(
                    "OpenAI API key not configured. Set QdrantSkills:OpenAiApiKey in config or OPENAI_API_KEY environment variable.");

            return new OpenAIClient(apiKey)
                .GetEmbeddingClient(options.EmbeddingModel)
                .AsIEmbeddingGenerator();
        });

        // IEmbeddingService -> OpenAiEmbeddingService
        services.AddSingleton<IEmbeddingService, OpenAiEmbeddingService>();

        // ISessionTracker -> InMemorySessionTracker (singleton -- one process = one session for stdio)
        services.AddSingleton<ISessionTracker, InMemorySessionTracker>();

        return services;
    }
}
