using System.Text.Json;
using System.Text.Json.Nodes;

namespace QdrantSkillsMCP.Infrastructure.Setup.Writers;

/// <summary>
/// Fallback writer for agents with unknown or unverified config formats.
/// Cannot auto-write; generates copy-paste JSON snippets instead.
/// </summary>
internal sealed class SnippetFallbackWriter : IAgentConfigWriter
{
    public string AgentName => "Other (Snippet)";
    public string WriterId => "snippet";
    public bool CanAutoWrite => false;
    public AgentScope[] SupportedScopes => [AgentScope.User, AgentScope.Project];
    public string? SkillDirectoryPath => null;

    public string? DetectInstallation(AgentScope scope) => null; // Never auto-detected
    public string? GetDefaultPath(AgentScope scope) => null; // Snippet-only, no file path

    public Task WriteConfigAsync(string configPath, McpServerEntry entry)
    {
        throw new NotSupportedException(
            "SnippetFallbackWriter does not support auto-write. Use GenerateSnippet() instead.");
    }

    public string GenerateSnippet(McpServerEntry entry, AgentScope scope)
    {
        var argsArray = new JsonArray();
        foreach (var arg in entry.Args)
            argsArray.Add(arg);

        var serverNode = new JsonObject
        {
            ["command"] = entry.Command,
            ["args"] = argsArray
        };

        var wrapper = new JsonObject
        {
            ["mcpServers"] = new JsonObject
            {
                [entry.ServerName] = serverNode
            }
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = wrapper.ToJsonString(options);

        return $"""
            Add the following to your agent's MCP configuration file:

            {json}

            Note: The root key may vary by agent (mcpServers, servers, mcp, etc.).
            Adjust accordingly for your specific agent.
            """;
    }
}
