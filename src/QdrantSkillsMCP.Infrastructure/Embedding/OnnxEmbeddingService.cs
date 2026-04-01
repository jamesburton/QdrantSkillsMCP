using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Embedding;

/// <summary>
/// Generates text embeddings using a local ONNX model (all-MiniLM-L6-v2)
/// via the SemanticKernel BertOnnxTextEmbeddingGenerationService bridge.
/// </summary>
public sealed class OnnxEmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    IOptions<QdrantSkillsOptions> options)
    : GeneratorEmbeddingServiceBase(generator)
{
    private readonly QdrantSkillsOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    /// <summary>Default dimensions for all-MiniLM-L6-v2.</summary>
    public const int DefaultOnnxDimensions = 384;

    /// <inheritdoc />
    public override int Dimensions
    {
        get
        {
            // Use configured dimensions only if explicitly set to a non-default value
            // that isn't the OpenAI default (1536) or the ONNX default (384).
            var modelDims = OnnxModelResolver.GetModelDimensions(_options.OnnxModelName);
            var configured = _options.VectorDimensions;
            if (configured != 1536 && configured != DefaultOnnxDimensions && configured != modelDims)
                return configured;
            return modelDims;
        }
    }
}
