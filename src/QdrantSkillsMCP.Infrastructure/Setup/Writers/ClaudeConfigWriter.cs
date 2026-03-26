namespace QdrantSkillsMCP.Infrastructure.Setup.Writers;

/// <summary>
/// Writes MCP config for Claude Code. JSON format with "mcpServers" root key.
/// User: ~/.claude.json, Project: .mcp.json
/// Skill directory: ~/.claude/skills/qdrant-skills-mcp/
/// </summary>
internal sealed class ClaudeConfigWriter : JsonConfigWriterBase
{
    public override string AgentName => "Claude Code";
    public override string WriterId => "claude";
    public override AgentScope[] SupportedScopes => [AgentScope.User, AgentScope.Project];
    protected override string RootKey => "mcpServers";

    public override string? SkillDirectoryPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "skills", "qdrant-skills-mcp");

    public override string? DetectInstallation(AgentScope scope)
    {
        var path = GetConfigPath(scope);
        return DetectIfExists(path);
    }

    private static string GetConfigPath(AgentScope scope) => scope switch
    {
        AgentScope.User => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude.json"),
        AgentScope.Project => Path.Combine(Directory.GetCurrentDirectory(), ".mcp.json"),
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };
}
