using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Embedding;

/// <summary>
/// Generates text embeddings using the <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> abstraction
/// backed by an OpenAI provider (text-embedding-3-small/large).
/// </summary>
public sealed class OpenAiEmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    IOptions<QdrantSkillsOptions> options)
    : GeneratorEmbeddingServiceBase(generator)
{
    private readonly QdrantSkillsOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public override int Dimensions => _options.VectorDimensions;
}
