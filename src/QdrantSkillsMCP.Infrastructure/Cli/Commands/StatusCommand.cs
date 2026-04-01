using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Cli.Commands;

/// <summary>
/// CLI command: status / info
/// </summary>
internal static class StatusCommand
{
    public static Task<int> RunAsync(
        IServiceProvider services, ConsoleOutputFormatter formatter, CancellationToken ct)
    {
        var options = services.GetRequiredService<IOptions<QdrantSkillsOptions>>().Value;

        var port = options.QdrantProtocol is "http" or "rest"
            ? options.QdrantRestPort
            : options.QdrantGrpcPort;

        formatter.FormatStatus(
            options.QdrantHost,
            port,
            options.CollectionName,
            options.EmbeddingProvider?.ToString(),
            options.VectorDimensions);

        return Task.FromResult(0);
    }
}
