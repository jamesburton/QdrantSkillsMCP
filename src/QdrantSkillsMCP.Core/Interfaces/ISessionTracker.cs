namespace QdrantSkillsMCP.Core.Interfaces;

/// <summary>
/// Tracks which skills have been loaded (full content returned) during the current session.
/// Used to populate the "ALREADY LOADED SKILLS" section in search responses.
/// </summary>
public interface ISessionTracker
{
    /// <summary>Marks a skill as loaded (full content returned) in the current session.</summary>
    void MarkLoaded(string skillName);

    /// <summary>Returns all skill names loaded in the current session.</summary>
    IReadOnlyList<string> GetLoadedSkills();

    /// <summary>Checks whether a skill has already been loaded in the current session.</summary>
    bool IsLoaded(string skillName);

    /// <summary>Clears all session state.</summary>
    void Reset();
}
