namespace QdrantSkillsMCP.Infrastructure.Configuration;

/// <summary>
/// Supported embedding provider backends.
/// </summary>
public enum EmbeddingProviderType
{
    /// <summary>Local ONNX model (default, runs in-process).</summary>
    LocalONNX,

    /// <summary>OpenAI embeddings API.</summary>
    OpenAI,

    /// <summary>Ollama local embeddings server.</summary>
    Ollama,

    /// <summary>Azure OpenAI embeddings endpoint.</summary>
    AzureOpenAI
}
