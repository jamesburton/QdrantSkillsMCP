namespace QdrantSkillsMCP.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration bound to the <c>QdrantSkills</c> config section.
/// Supports appsettings.json, user secrets, and environment variables
/// (prefix: <c>QDRANT_SKILLS__</c>).
/// </summary>
public sealed class QdrantSkillsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "QdrantSkills";

    /// <summary>Qdrant server hostname. Default: localhost.</summary>
    public string QdrantHost { get; set; } = "localhost";

    /// <summary>Qdrant gRPC port. Default: 6334.</summary>
    public int QdrantGrpcPort { get; set; } = 6334;

    /// <summary>Optional Qdrant API key for authenticated instances.</summary>
    public string? QdrantApiKey { get; set; }

    /// <summary>Whether to use HTTPS/TLS for Qdrant connection. Default: false (auto-detected for remote hosts).</summary>
    public bool UseTls { get; set; }

    /// <summary>Qdrant collection name. Default: skills.</summary>
    public string CollectionName { get; set; } = "skills";

    /// <summary>Vector dimensions for the configured embedding model. Default: 1536 (text-embedding-3-small).</summary>
    public int VectorDimensions { get; set; } = 1536;

    /// <summary>OpenAI embedding model name. Default: text-embedding-3-small.</summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>Optional OpenAI API key. Can also be set via OPENAI_API_KEY environment variable.</summary>
    public string? OpenAiApiKey { get; set; }

    // --- Embedding provider configuration (Phase 2) ---

    /// <summary>
    /// Embedding provider backend. Null means default to LocalONNX with a startup warning.
    /// </summary>
    public EmbeddingProviderType? EmbeddingProvider { get; set; }

    /// <summary>URL override for Ollama embedding endpoint (e.g. http://localhost:11434).</summary>
    public string? EmbeddingUrl { get; set; }

    /// <summary>
    /// ONNX model name to use for local embeddings.
    /// Supported: "all-MiniLM-L6-v2" (default), "bge-small-en-v1.5", "bge-base-en-v1.5".
    /// </summary>
    public string? OnnxModelName { get; set; }

    /// <summary>Explicit path to a custom ONNX model file. Null uses the bundled default.</summary>
    public string? OnnxModelPath { get; set; }

    /// <summary>When true, disables automatic HuggingFace model download for ONNX models.</summary>
    public bool DisableAutoDownload { get; set; }

    // --- Azure OpenAI configuration ---

    /// <summary>Azure OpenAI endpoint URL.</summary>
    public string? AzureOpenAiEndpoint { get; set; }

    /// <summary>Azure OpenAI API key.</summary>
    public string? AzureOpenAiApiKey { get; set; }

    /// <summary>Azure OpenAI deployment name.</summary>
    public string? AzureOpenAiDeployment { get; set; }

    // --- Validation and migration ---

    /// <summary>When true, skips embedding output dimension validation at startup.</summary>
    public bool SkipEmbeddingOutputValidation { get; set; }

    /// <summary>Key used for the test embedding request at startup. Default: "test".</summary>
    public string TestEmbeddingKey { get; set; } = "test";

    /// <summary>Input string used for the test embedding request at startup.</summary>
    public string TestEmbeddingInput { get; set; } = "This is a test embedding input string.";

    /// <summary>
    /// Strategy when collection vector dimensions mismatch configured dimensions.
    /// Values: "rename", "suffix", "replace", or null (hard fail).
    /// </summary>
    public string? MismatchResolution { get; set; }
}
