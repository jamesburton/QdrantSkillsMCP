namespace QdrantSkillsMCP.Infrastructure.Setup.Writers;

/// <summary>
/// Writes MCP config for KiloCode. JSON format with "mcpServers" root key.
/// User: ~/.kilocode/mcp_settings.json, Project: .kilocode/mcp.json
/// </summary>
internal sealed class KiloCodeConfigWriter : JsonConfigWriterBase
{
    public override string AgentName => "KiloCode";
    public override string WriterId => "kilocode";
    public override AgentScope[] SupportedScopes => [AgentScope.User, AgentScope.Project];
    protected override string RootKey => "mcpServers";

    public override string? DetectInstallation(AgentScope scope)
    {
        var path = GetConfigPath(scope);
        return DetectIfExists(path);
    }

    private static string GetConfigPath(AgentScope scope) => scope switch
    {
        AgentScope.User => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".kilocode", "mcp_settings.json"),
        AgentScope.Project => Path.Combine(Directory.GetCurrentDirectory(), ".kilocode", "mcp.json"),
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };
}
