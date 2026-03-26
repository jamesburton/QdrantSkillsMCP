using System.Text.Json;
using System.Text.Json.Nodes;

namespace QdrantSkillsMCP.Infrastructure.Setup.Writers;

/// <summary>
/// Base class for JSON-based agent config writers. Handles backup, read-modify-write,
/// and validation using System.Text.Json.Nodes.JsonNode.
/// </summary>
internal abstract class JsonConfigWriterBase : IAgentConfigWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    public abstract string AgentName { get; }
    public abstract string WriterId { get; }
    public virtual bool CanAutoWrite => true;
    public abstract AgentScope[] SupportedScopes { get; }
    public abstract string? DetectInstallation(AgentScope scope);
    public virtual string? SkillDirectoryPath => null;

    /// <summary>The root key under which MCP servers are stored (e.g., "mcpServers", "servers", "mcp").</summary>
    protected abstract string RootKey { get; }

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
            // Ensure directory exists for new files
            var dir = Path.GetDirectoryName(configPath);
            if (dir is not null)
                Directory.CreateDirectory(dir);
            root = new JsonObject();
        }

        // Ensure the root key object exists
        if (root[RootKey] is not JsonObject)
        {
            root[RootKey] = new JsonObject();
        }

        // Build the server entry node
        var serverNode = BuildServerNode(entry);

        // Merge: set the server entry under root key
        root[RootKey]![entry.ServerName] = serverNode;

        // Write with indentation
        var output = root.ToJsonString(WriteOptions);
        await File.WriteAllTextAsync(configPath, output);

        // Validate by parsing back
        JsonNode.Parse(await File.ReadAllTextAsync(configPath));
    }

    /// <summary>
    /// Builds the JSON node for the server entry. Override to add agent-specific fields
    /// like "type": "stdio" or "type": "local".
    /// </summary>
    protected virtual JsonObject BuildServerNode(McpServerEntry entry)
    {
        var argsArray = new JsonArray();
        foreach (var arg in entry.Args)
            argsArray.Add(arg);

        return new JsonObject
        {
            ["command"] = entry.Command,
            ["args"] = argsArray
        };
    }

    public virtual string GenerateSnippet(McpServerEntry entry, AgentScope scope)
    {
        var node = BuildServerNode(entry);
        var wrapper = new JsonObject
        {
            [RootKey] = new JsonObject
            {
                [entry.ServerName] = node
            }
        };
        return wrapper.ToJsonString(WriteOptions);
    }

    /// <summary>
    /// Helper to detect if a config file or its parent directory exists.
    /// </summary>
    protected static string? DetectIfExists(string path)
    {
        if (File.Exists(path))
            return path;

        // Check if parent directory exists (agent is installed but config not yet created)
        var dir = Path.GetDirectoryName(path);
        if (dir is not null && Directory.Exists(dir))
            return path;

        return null;
    }
}
