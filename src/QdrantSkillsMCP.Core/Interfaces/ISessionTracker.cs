namespace QdrantSkillsMCP.Core.Interfaces;

/// <summary>
/// Tracks which skills have been loaded (full content returned) during the current session.
/// Used to populate the "ALREADY LOADED SKILLS" section in search responses.
/// Supports keyed sessions via optional <paramref name="sessionId"/> parameter.
/// </summary>
public interface ISessionTracker
{
    /// <summary>Marks a skill as loaded (full content returned) in the specified session.</summary>
    void MarkLoaded(string skillName, string? sessionId = null);

    /// <summary>Returns all skill names loaded in the specified session.</summary>
    IReadOnlyList<string> GetLoadedSkills(string? sessionId = null);

    /// <summary>Checks whether a skill has already been loaded in the specified session.</summary>
    bool IsLoaded(string skillName, string? sessionId = null);

    /// <summary>Clears all session state for the specified session (or default if null).</summary>
    void Reset(string? sessionId = null);
}
