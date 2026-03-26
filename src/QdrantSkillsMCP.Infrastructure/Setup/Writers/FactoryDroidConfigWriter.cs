using System.Text.Json.Nodes;

namespace QdrantSkillsMCP.Infrastructure.Setup.Writers;

/// <summary>
/// Writes MCP config for factory-droid. JSON format with "mcpServers" root key and "type":"stdio".
/// User: ~/.factory/mcp.json, Project: .factory/mcp.json
/// </summary>
internal sealed class FactoryDroidConfigWriter : JsonConfigWriterBase
{
    public override string AgentName => "factory-droid";
    public override string WriterId => "factory-droid";
    public override AgentScope[] SupportedScopes => [AgentScope.User, AgentScope.Project];
    protected override string RootKey => "mcpServers";

    public override string? DetectInstallation(AgentScope scope)
    {
        var path = GetConfigPath(scope);
        return DetectIfExists(path);
    }

    protected override JsonObject BuildServerNode(McpServerEntry entry)
    {
        var node = base.BuildServerNode(entry);
        node["type"] = "stdio";
        return node;
    }

    private static string GetConfigPath(AgentScope scope) => scope switch
    {
        AgentScope.User => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".factory", "mcp.json"),
        AgentScope.Project => Path.Combine(Directory.GetCurrentDirectory(), ".factory", "mcp.json"),
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };
}
