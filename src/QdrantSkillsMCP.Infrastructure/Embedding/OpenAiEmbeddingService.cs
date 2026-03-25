using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Embedding;

/// <summary>
/// Generates text embeddings using the <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> abstraction
/// backed by an OpenAI provider (text-embedding-3-small/large).
/// </summary>
public sealed class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly QdrantSkillsOptions _options;

    public OpenAiEmbeddingService(
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
