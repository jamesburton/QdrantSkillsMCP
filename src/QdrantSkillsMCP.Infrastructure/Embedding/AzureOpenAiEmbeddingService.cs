using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Embedding;

/// <summary>
/// Generates text embeddings using Azure OpenAI via the Azure.AI.OpenAI client.
/// Requires AzureOpenAiEndpoint, AzureOpenAiApiKey, and AzureOpenAiDeployment config.
/// </summary>
public sealed class AzureOpenAiEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly QdrantSkillsOptions _options;

    public AzureOpenAiEmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        IOptions<QdrantSkillsOptions> options)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public int Dimensions => _options.VectorDimensions;

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var result = await _generator.GenerateAsync([text], cancellationToken: ct);
        return result[0].Vector.ToArray();
    }
}
