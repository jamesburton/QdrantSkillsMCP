using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Embedding;

/// <summary>
/// Generates text embeddings using Azure OpenAI via the Azure.AI.OpenAI client.
/// Requires AzureOpenAiEndpoint, AzureOpenAiApiKey, and AzureOpenAiDeployment config.
/// </summary>
public sealed class AzureOpenAiEmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    IOptions<QdrantSkillsOptions> options)
    : GeneratorEmbeddingServiceBase(generator)
{
    private readonly QdrantSkillsOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public override int Dimensions => _options.VectorDimensions;
}
