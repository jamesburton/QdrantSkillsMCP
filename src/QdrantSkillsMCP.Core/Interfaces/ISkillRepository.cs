using QdrantSkillsMCP.Core.Models;

namespace QdrantSkillsMCP.Core.Interfaces;

/// <summary>
/// Contract for skill persistence in Qdrant.
/// All operations use the skill <see cref="Skill.Name"/> as the logical primary key.
/// </summary>
public interface ISkillRepository
{
    /// <summary>Adds a new skill with its embedding vector. Throws if duplicate unless <paramref name="overwrite"/> is true.</summary>
    Task AddAsync(Skill skill, float[] embedding, bool overwrite, CancellationToken ct);

    /// <summary>Updates an existing skill and its embedding vector.</summary>
    Task UpdateAsync(Skill skill, float[] embedding, CancellationToken ct);

    /// <summary>Permanently deletes a skill by name.</summary>
    Task DeleteAsync(string skillName, CancellationToken ct);

    /// <summary>Soft-hides a skill by setting its Archived flag (excluded from search, restorable).</summary>
    Task ArchiveAsync(string skillName, CancellationToken ct);

    /// <summary>Retrieves a skill by exact name, or null if not found.</summary>
    Task<Skill?> GetByNameAsync(string skillName, CancellationToken ct);

    /// <summary>Performs a semantic vector search and returns results ordered by score descending.</summary>
    Task<IReadOnlyList<SearchResult>> SearchAsync(float[] queryEmbedding, int maxResults, float? scoreThreshold, CancellationToken ct);

    /// <summary>Lists metadata for all non-archived skills.</summary>
    Task<IReadOnlyList<SkillMetadata>> ListAsync(CancellationToken ct);

    /// <summary>Ensures the Qdrant collection exists with the correct vector configuration.</summary>
    Task EnsureCollectionAsync(CancellationToken ct);
}
