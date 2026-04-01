using Microsoft.Extensions.AI;
using QdrantSkillsMCP.Core.Interfaces;

namespace QdrantSkillsMCP.Infrastructure.Embedding;

/// <summary>
/// Base class for embedding services backed by an IEmbeddingGenerator.
/// Eliminates boilerplate from OpenAI, Ollama, AzureOpenAI, and ONNX service implementations.
/// </summary>
public abstract class GeneratorEmbeddingServiceBase(
    IEmbeddingGenerator<string, Embedding<float>> generator) : IEmbeddingService
{
    public abstract int Dimensions { get; }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var result = await generator.GenerateAsync([text], cancellationToken: ct);
        return result[0].Vector.ToArray();
    }
}
