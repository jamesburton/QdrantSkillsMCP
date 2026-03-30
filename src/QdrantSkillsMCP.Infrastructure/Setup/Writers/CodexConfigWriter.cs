using Tomlyn;
using Tomlyn.Model;

namespace QdrantSkillsMCP.Infrastructure.Setup.Writers;

/// <summary>
/// Writes MCP config for OpenAI Codex CLI. TOML format with [mcp_servers.name] sections.
/// User: ~/.codex/config.toml, Project: .codex/config.toml
/// </summary>
internal sealed class CodexConfigWriter : IAgentConfigWriter
{
    public string AgentName => "OpenAI Codex";
    public string WriterId => "codex";
    public bool CanAutoWrite => true;
    public AgentScope[] SupportedScopes => [AgentScope.User, AgentScope.Project];
    public string? SkillDirectoryPath => null;

    public string? DetectInstallation(AgentScope scope)
    {
        var path = GetConfigPath(scope);
        if (File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path);
        if (dir is not null && Directory.Exists(dir)) return path;
        return null;
    }

    public string? GetDefaultPath(AgentScope scope) => GetConfigPath(scope);

    public async Task WriteConfigAsync(string configPath, McpServerEntry entry)
    {
        // Backup existing file
        if (File.Exists(configPath))
        {
            File.Copy(configPath, configPath + ".bak", overwrite: true);
        }

        // Read or create TOML model
        TomlTable root;
        if (File.Exists(configPath))
        {
            var existingToml = await File.ReadAllTextAsync(configPath);
            root = Toml.ToModel(existingToml);
        }
        else
        {
            var dir = Path.GetDirectoryName(configPath);
            if (dir is not null)
                Directory.CreateDirectory(dir);
            root = new TomlTable();
        }

        // Ensure mcp_servers table exists
        if (!root.ContainsKey("mcp_servers") || root["mcp_servers"] is not TomlTable)
        {
            root["mcp_servers"] = new TomlTable();
        }

        var mcpServers = (TomlTable)root["mcp_servers"];

        // Create the server entry table
        var serverTable = new TomlTable
        {
            ["command"] = entry.Command,
            ["args"] = new TomlArray { entry.Args[0] }
        };

        mcpServers[entry.ServerName] = serverTable;

        // Write TOML
        var output = Toml.FromModel(root);
        await File.WriteAllTextAsync(configPath, output);
    }

    public string GenerateSnippet(McpServerEntry entry, AgentScope scope)
    {
        return $"""
            [mcp_servers.{entry.ServerName}]
            command = "{entry.Command}"
            args = ["{string.Join("\", \"", entry.Args)}"]
            """;
    }

    private static string GetConfigPath(AgentScope scope) => scope switch
    {
        AgentScope.User => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex", "config.toml"),
        AgentScope.Project => Path.Combine(Directory.GetCurrentDirectory(), ".codex", "config.toml"),
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };
}
