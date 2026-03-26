namespace QdrantSkillsMCP.Infrastructure.Setup;

/// <summary>
/// Contract for writing MCP server configuration entries into an agent's config file.
/// Each implementation handles a specific agent's config format and paths.
/// </summary>
public interface IAgentConfigWriter
{
    /// <summary>Human-readable agent name (e.g., "Claude Code", "VS Code / Copilot").</summary>
    string AgentName { get; }

    /// <summary>Unique key for matching (e.g., "claude", "copilot").</summary>
    string WriterId { get; }

    /// <summary>Whether this writer can auto-write config (false = snippet-only).</summary>
    bool CanAutoWrite { get; }

    /// <summary>Scopes supported by this agent (User, Project, or both).</summary>
    AgentScope[] SupportedScopes { get; }

    /// <summary>
    /// Probes the filesystem for this agent's config at the given scope.
    /// Returns the config file path if the agent is detected, null otherwise.
    /// </summary>
    string? DetectInstallation(AgentScope scope);

    /// <summary>
    /// Path where SKILL.md should be placed for this agent, or null if the agent
    /// does not support skill directories.
    /// </summary>
    string? SkillDirectoryPath { get; }

    /// <summary>
    /// Writes (merges) the MCP server entry into the agent's config file.
    /// Creates a .bak backup before modifying existing files.
    /// </summary>
    Task WriteConfigAsync(string configPath, McpServerEntry entry);

    /// <summary>
    /// Generates a copy-paste snippet for manual configuration.
    /// </summary>
    string GenerateSnippet(McpServerEntry entry, AgentScope scope);
}
