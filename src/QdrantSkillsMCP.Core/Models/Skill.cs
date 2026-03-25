namespace QdrantSkillsMCP.Core.Models;

/// <summary>
/// Represents a skill document stored in Qdrant.
/// The <see cref="Name"/> is the primary key (deterministic SHA-256 hash produces the Qdrant point ID).
/// </summary>
public sealed record Skill
{
    /// <summary>Unique skill identifier (lowercase letters, numbers, hyphens; max 64 chars).</summary>
    public required string Name { get; init; }

    /// <summary>Optional short description from YAML frontmatter.</summary>
    public string? Description { get; init; }

    /// <summary>Optional tags from YAML frontmatter.</summary>
    public string[]? Tags { get; init; }

    /// <summary>Original markdown content including YAML frontmatter (lossless round-trip).</summary>
    public required string RawContent { get; init; }

    /// <summary>Markdown body only (frontmatter stripped).</summary>
    public required string MarkdownBody { get; init; }

    /// <summary>Last time the skill was added or updated.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>When true the skill is excluded from search results but not deleted.</summary>
    public bool Archived { get; init; }
}
