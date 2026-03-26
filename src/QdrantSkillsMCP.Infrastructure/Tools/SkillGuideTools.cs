using System.ComponentModel;
using ModelContextProtocol.Server;

namespace QdrantSkillsMCP.Infrastructure.Tools;

/// <summary>
/// MCP tool that returns the bundled SKILL.md agent teaching guide.
/// </summary>
[McpServerToolType]
public sealed class SkillGuideTools
{
    /// <summary>
    /// Returns the QdrantSkillsMCP usage guide that teaches agents how to effectively
    /// use this server, including the search-before-load pattern, output modes, and session tracking.
    /// </summary>
    [McpServerTool(Name = "get-skill-guide", ReadOnly = true)]
    [Description("Returns the QdrantSkillsMCP usage guide that teaches agents how to effectively use this server")]
    public string GetSkillGuide()
    {
        var assembly = typeof(SkillGuideTools).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "QdrantSkillsMCP.Infrastructure.SkillGuide.SKILL.md");

        if (stream is null)
            throw new InvalidOperationException("SKILL.md embedded resource not found");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
