using System.Text.Json.Nodes;

namespace QdrantSkillsMCP.Infrastructure.Setup.Writers;

/// <summary>
/// Writes MCP config for GitHub Copilot CLI. JSON format with "mcpServers" root key.
/// User: ~/.copilot/mcp-config.json. User-level only.
/// </summary>
internal sealed class CopilotCliConfigWriter : JsonConfigWriterBase
{
    public override string AgentName => "Copilot CLI";
    public override string WriterId => "copilot-cli";
    public override AgentScope[] SupportedScopes => [AgentScope.User];
    protected override string RootKey => "mcpServers";

    public override string? DetectInstallation(AgentScope scope)
    {
        if (scope != AgentScope.User) return null;
        return DetectIfExists(GetConfigPath());
    }

    public override string? GetDefaultPath(AgentScope scope) =>
        scope == AgentScope.User ? GetConfigPath() : null;

    protected override JsonObject BuildServerNode(McpServerEntry entry)
    {
        var node = base.BuildServerNode(entry);
        node["type"] = "local";
        return node;
    }

    private static string GetConfigPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".copilot", "mcp-config.json");
}
