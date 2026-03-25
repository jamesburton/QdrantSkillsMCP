namespace QdrantSkillsMCP.Core.Models;

/// <summary>
/// Wraps a <see cref="Models.Skill"/> with its similarity score from a vector search.
/// </summary>
public sealed record SearchResult
{
    /// <summary>The matched skill.</summary>
    public required Skill Skill { get; init; }

    /// <summary>Cosine similarity score (0-1).</summary>
    public required float Score { get; init; }
}
