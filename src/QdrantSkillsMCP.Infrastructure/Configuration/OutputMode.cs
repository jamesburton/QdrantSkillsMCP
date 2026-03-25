namespace QdrantSkillsMCP.Infrastructure.Configuration;

/// <summary>
/// Controls the level of detail returned by search and list tools.
/// </summary>
public enum OutputMode
{
    /// <summary>Full skill content (marks skills as loaded in session).</summary>
    Full,

    /// <summary>Skill names only (does not mark as loaded).</summary>
    Names,

    /// <summary>Name + description per skill (does not mark as loaded).</summary>
    Summaries
}
