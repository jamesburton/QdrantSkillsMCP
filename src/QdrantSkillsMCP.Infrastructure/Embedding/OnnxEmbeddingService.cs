using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Embedding;

/// <summary>
/// Generates text embeddings using a local ONNX model (all-MiniLM-L6-v2)
/// via the SemanticKernel BertOnnxTextEmbeddingGenerationService bridge.
/// </summary>
public sealed class OnnxEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly QdrantSkillsOptions _options;

    /// <summary>Default dimensions for all-MiniLM-L6-v2.</summary>
    public const int DefaultOnnxDimensions = 384;

    public OnnxEmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        IOptions<QdrantSkillsOptions> options)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public int Dimensions
    {
        get
        {
            // Use configured dimensions only if explicitly set to a non-default value
            // that isn't the OpenAI default (1536) or the ONNX default (384).
            var configured = _options.VectorDimensions;
            if (configured != 1536 && configured != DefaultOnnxDimensions)
                return configured;
            return DefaultOnnxDimensions;
        }
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var result = await _generator.GenerateAsync([text], cancellationToken: ct);
        return result[0].Vector.ToArray();
    }
}
