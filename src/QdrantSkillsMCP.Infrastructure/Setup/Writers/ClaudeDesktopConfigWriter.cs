namespace QdrantSkillsMCP.Infrastructure.Setup.Writers;

/// <summary>
/// Writes MCP config for Claude Desktop. JSON format with "mcpServers" root key.
/// Windows: %APPDATA%/Claude/claude_desktop_config.json
/// macOS: ~/Library/Application Support/Claude/claude_desktop_config.json
/// User-level only. No skill directory support.
/// </summary>
internal sealed class ClaudeDesktopConfigWriter : JsonConfigWriterBase
{
    public override string AgentName => "Claude Desktop";
    public override string WriterId => "claude-desktop";
    public override AgentScope[] SupportedScopes => [AgentScope.User];
    protected override string RootKey => "mcpServers";

    public override string? DetectInstallation(AgentScope scope)
    {
        if (scope != AgentScope.User) return null;
        var path = GetConfigPath();
        return DetectIfExists(path);
    }

    private static string GetConfigPath()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Claude", "claude_desktop_config.json");
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "Claude", "claude_desktop_config.json");
        }

        // Linux: use XDG config or fallback
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Claude", "claude_desktop_config.json");
    }
}
