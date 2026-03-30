using System.Text.Json;
using System.Text.Json.Nodes;

namespace QdrantSkillsMCP.Infrastructure.Setup.Writers;

/// <summary>
/// Writes MCP config for Zed. JSON format nested under assistant.context_servers.
/// User: ~/.config/zed/settings.json
/// Format: { "assistant": { "context_servers": { "name": { "command": { "path": "...", "args": [...] } } } } }
/// </summary>
internal sealed class ZedConfigWriter : IAgentConfigWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public string AgentName => "Zed";
    public string WriterId => "zed";
    public bool CanAutoWrite => true;
    public AgentScope[] SupportedScopes => [AgentScope.User];
    public string? SkillDirectoryPath => null;

    public string? DetectInstallation(AgentScope scope)
    {
        if (scope != AgentScope.User) return null;
        var path = GetConfigPath();
        if (File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path);
        if (dir is not null && Directory.Exists(dir)) return path;
        return null;
    }

    public string? GetDefaultPath(AgentScope scope) =>
        scope == AgentScope.User ? GetConfigPath() : null;

    public async Task WriteConfigAsync(string configPath, McpServerEntry entry)
    {
        // Backup existing file
        if (File.Exists(configPath))
        {
            File.Copy(configPath, configPath + ".bak", overwrite: true);
        }

        // Read or create root object
        JsonNode root;
        if (File.Exists(configPath))
        {
            var existingJson = await File.ReadAllTextAsync(configPath);
            root = JsonNode.Parse(existingJson) ?? new JsonObject();
        }
        else
        {
            var dir = Path.GetDirectoryName(configPath);
            if (dir is not null)
                Directory.CreateDirectory(dir);
            root = new JsonObject();
        }

        // Ensure assistant object exists
        if (root["assistant"] is not JsonObject)
        {
            root["assistant"] = new JsonObject();
        }

        var assistant = root["assistant"]!.AsObject();

        // Ensure context_servers object exists
        if (assistant["context_servers"] is not JsonObject)
        {
            assistant["context_servers"] = new JsonObject();
        }

        var contextServers = assistant["context_servers"]!.AsObject();

        // Build the args array
        var argsArray = new JsonArray();
        foreach (var arg in entry.Args)
            argsArray.Add(arg);

        // Write the server entry with nested command object
        contextServers[entry.ServerName] = new JsonObject
        {
            ["command"] = new JsonObject
            {
                ["path"] = entry.Command,
                ["args"] = argsArray
            }
        };

        var output = root.ToJsonString(WriteOptions);
        await File.WriteAllTextAsync(configPath, output);

        // Validate by parsing back
        JsonNode.Parse(await File.ReadAllTextAsync(configPath));
    }

    public string GenerateSnippet(McpServerEntry entry, AgentScope scope)
    {
        var argsArray = new JsonArray();
        foreach (var arg in entry.Args)
            argsArray.Add(arg);

        var wrapper = new JsonObject
        {
            ["assistant"] = new JsonObject
            {
                ["context_servers"] = new JsonObject
                {
                    [entry.ServerName] = new JsonObject
                    {
                        ["command"] = new JsonObject
                        {
                            ["path"] = entry.Command,
                            ["args"] = argsArray
                        }
                    }
                }
            }
        };
        return wrapper.ToJsonString(WriteOptions);
    }

    private static string GetConfigPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "zed", "settings.json");
}
