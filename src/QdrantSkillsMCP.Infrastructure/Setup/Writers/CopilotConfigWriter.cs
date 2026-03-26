using System.Text.Json.Nodes;

namespace QdrantSkillsMCP.Infrastructure.Setup.Writers;

/// <summary>
/// Writes MCP config for VS Code / GitHub Copilot. JSON format with "servers" root key
/// (NOT "mcpServers"). Project: .vscode/mcp.json
/// </summary>
internal sealed class CopilotConfigWriter : JsonConfigWriterBase
{
    public override string AgentName => "VS Code / Copilot";
    public override string WriterId => "copilot";
    public override AgentScope[] SupportedScopes => [AgentScope.Project];
    protected override string RootKey => "servers";

    public override string? DetectInstallation(AgentScope scope)
    {
        if (scope != AgentScope.Project) return null;
        var path = GetConfigPath();
        return DetectIfExists(path);
    }

    protected override JsonObject BuildServerNode(McpServerEntry entry)
    {
        var node = base.BuildServerNode(entry);
        node["type"] = "stdio";
        return node;
    }

    private static string GetConfigPath() =>
        Path.Combine(Directory.GetCurrentDirectory(), ".vscode", "mcp.json");
}
