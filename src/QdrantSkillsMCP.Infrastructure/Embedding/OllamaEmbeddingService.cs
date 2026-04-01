using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Embedding;

/// <summary>
/// Generates text embeddings using Ollama via the OllamaSharp client.
/// The user must configure VectorDimensions to match their chosen Ollama model.
/// </summary>
public sealed class OllamaEmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    IOptions<QdrantSkillsOptions> options)
    : GeneratorEmbeddingServiceBase(generator)
{
    private readonly QdrantSkillsOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public override int Dimensions => _options.VectorDimensions;
}
