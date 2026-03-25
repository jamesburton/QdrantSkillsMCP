namespace QdrantSkillsMCP.Core.Models;

/// <summary>
/// Lightweight skill representation returned when <c>includeContent: false</c>.
/// Contains only metadata — the agent must call load-skill separately to get full content.
/// </summary>
public sealed record SkillMetadata
{
    /// <summary>Unique skill identifier.</summary>
    public required string Name { get; init; }

    /// <summary>Optional short description from YAML frontmatter.</summary>
    public string? Description { get; init; }

    /// <summary>Optional tags from YAML frontmatter.</summary>
    public string[]? Tags { get; init; }

    /// <summary>Similarity score (0-1) when returned from a search operation.</summary>
    public float Score { get; init; }

    /// <summary>Last time the skill was added or updated.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
