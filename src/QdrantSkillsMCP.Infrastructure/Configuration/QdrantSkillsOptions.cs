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

    /// <summary>Qdrant collection name. Default: skills.</summary>
    public string CollectionName { get; set; } = "skills";

    /// <summary>Vector dimensions for the configured embedding model. Default: 1536 (text-embedding-3-small).</summary>
    public int VectorDimensions { get; set; } = 1536;

    /// <summary>OpenAI embedding model name. Default: text-embedding-3-small.</summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>Optional OpenAI API key. Can also be set via OPENAI_API_KEY environment variable.</summary>
    public string? OpenAiApiKey { get; set; }
}
