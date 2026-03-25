using System.Collections.Concurrent;
using QdrantSkillsMCP.Core.Interfaces;

namespace QdrantSkillsMCP.Infrastructure.Session;

/// <summary>
/// In-process session tracker supporting keyed sessions.
/// When <c>sessionId</c> is null, operations target the default session.
/// Thread-safe via nested <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Registered as singleton in DI.
/// </summary>
public sealed class InMemorySessionTracker : ISessionTracker
{
    internal const string DefaultSessionId = "__default__";

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _sessions = new(StringComparer.Ordinal);

    private ConcurrentDictionary<string, byte> GetSession(string? sessionId)
    {
        var key = sessionId ?? DefaultSessionId;
        return _sessions.GetOrAdd(key, _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public void MarkLoaded(string skillName, string? sessionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillName);
        GetSession(sessionId).TryAdd(skillName, 0);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetLoadedSkills(string? sessionId = null)
    {
        var key = sessionId ?? DefaultSessionId;
        if (!_sessions.TryGetValue(key, out var session))
            return Array.Empty<string>();

        return session.Keys.Order().ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public bool IsLoaded(string skillName, string? sessionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillName);
        var key = sessionId ?? DefaultSessionId;
        if (!_sessions.TryGetValue(key, out var session))
            return false;

        return session.ContainsKey(skillName);
    }

    /// <inheritdoc />
    public void Reset(string? sessionId = null)
    {
        var key = sessionId ?? DefaultSessionId;
        if (_sessions.TryGetValue(key, out var session))
            session.Clear();
    }
}
