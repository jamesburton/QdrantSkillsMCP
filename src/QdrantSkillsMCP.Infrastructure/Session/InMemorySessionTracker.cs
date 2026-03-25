using System.Collections.Concurrent;
using QdrantSkillsMCP.Core.Interfaces;

namespace QdrantSkillsMCP.Infrastructure.Session;

/// <summary>
/// In-process session tracker for stdio transport (one process = one session).
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Registered as singleton in DI.
/// </summary>
public sealed class InMemorySessionTracker : ISessionTracker
{
    private readonly ConcurrentDictionary<string, byte> _loadedSkills = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void MarkLoaded(string skillName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillName);
        _loadedSkills.TryAdd(skillName, 0);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetLoadedSkills()
    {
        return _loadedSkills.Keys.Order().ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public bool IsLoaded(string skillName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillName);
        return _loadedSkills.ContainsKey(skillName);
    }

    /// <inheritdoc />
    public void Reset()
    {
        _loadedSkills.Clear();
    }
}
