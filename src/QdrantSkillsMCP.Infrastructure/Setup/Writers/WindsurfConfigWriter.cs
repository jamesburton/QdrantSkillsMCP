namespace QdrantSkillsMCP.Infrastructure.Setup.Writers;

/// <summary>
/// Writes MCP config for Windsurf. JSON format with "mcpServers" root key.
/// User: ~/.codeium/windsurf/mcp_config.json
/// </summary>
internal sealed class WindsurfConfigWriter : JsonConfigWriterBase
{
    public override string AgentName => "Windsurf";
    public override string WriterId => "windsurf";
    public override AgentScope[] SupportedScopes => [AgentScope.User];
    protected override string RootKey => "mcpServers";

    public override string? DetectInstallation(AgentScope scope)
    {
        if (scope != AgentScope.User) return null;
        var path = GetConfigPath();
        return DetectIfExists(path);
    }

    private static string GetConfigPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codeium", "windsurf", "mcp_config.json");
}
