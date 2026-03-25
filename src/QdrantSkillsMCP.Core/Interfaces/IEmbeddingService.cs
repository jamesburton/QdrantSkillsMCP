namespace QdrantSkillsMCP.Core.Interfaces;

/// <summary>
/// Contract for generating text embeddings used for semantic search.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>Generates an embedding vector for the given text.</summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct);

    /// <summary>The number of dimensions produced by the configured embedding model (used for collection creation).</summary>
    int Dimensions { get; }
}
