namespace QdrantSkillsMCP.Infrastructure.Setup.Writers;

/// <summary>
/// Writes MCP config for Cursor. JSON format with "mcpServers" root key.
/// Project: .cursor/mcp.json
/// </summary>
internal sealed class CursorConfigWriter : JsonConfigWriterBase
{
    public override string AgentName => "Cursor";
    public override string WriterId => "cursor";
    public override AgentScope[] SupportedScopes => [AgentScope.Project];
    protected override string RootKey => "mcpServers";

    public override string? DetectInstallation(AgentScope scope)
    {
        if (scope != AgentScope.Project) return null;
        var path = GetConfigPath();
        return DetectIfExists(path);
    }

    private static string GetConfigPath() =>
        Path.Combine(Directory.GetCurrentDirectory(), ".cursor", "mcp.json");
}
