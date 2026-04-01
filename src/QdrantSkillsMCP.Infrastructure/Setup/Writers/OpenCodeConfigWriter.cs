using System.Text.Json.Nodes;

namespace QdrantSkillsMCP.Infrastructure.Setup.Writers;

/// <summary>
/// Writes MCP config for opencode. JSON format with "mcp" root key and command as array.
/// User: ~/.config/opencode/opencode.json (or %APPDATA%/opencode on Windows)
/// Project: opencode.json
/// </summary>
internal sealed class OpenCodeConfigWriter : JsonConfigWriterBase
{
    public override string AgentName => "opencode";
    public override string WriterId => "opencode";
    public override AgentScope[] SupportedScopes => [AgentScope.User, AgentScope.Project];
    protected override string RootKey => "mcp";

    public override string? DetectInstallation(AgentScope scope) => DetectIfExists(GetConfigPath(scope));

    public override string? GetDefaultPath(AgentScope scope) => GetConfigPath(scope);

    protected override JsonObject BuildServerNode(McpServerEntry entry)
    {
        // opencode uses command as array: ["dnx", "QdrantSkillsMCP"]
        var commandArray = new JsonArray { entry.Command };
        foreach (var arg in entry.Args)
            commandArray.Add(arg);

        return new JsonObject
        {
            ["command"] = commandArray,
            ["type"] = "local"
        };
    }

    private static string GetConfigPath(AgentScope scope)
    {
        if (scope == AgentScope.Project)
            return Path.Combine(Directory.GetCurrentDirectory(), "opencode.json");

        // User level
        if (OperatingSystem.IsWindows())
        {
            // Check both APPDATA and LOCALAPPDATA
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = Path.Combine(appData, "opencode", "opencode.json");
            if (File.Exists(path) || Directory.Exists(Path.GetDirectoryName(path)!))
                return path;

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "opencode", "opencode.json");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "opencode", "opencode.json");
    }
}
