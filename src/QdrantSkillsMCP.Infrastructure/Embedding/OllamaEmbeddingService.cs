using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Embedding;

/// <summary>
/// Generates text embeddings using Ollama via the OllamaSharp client.
/// The user must configure VectorDimensions to match their chosen Ollama model.
/// </summary>
public sealed class OllamaEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly QdrantSkillsOptions _options;

    public OllamaEmbeddingService(
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
