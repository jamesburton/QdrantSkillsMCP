namespace QdrantSkillsMCP.Infrastructure.Setup;

/// <summary>
/// Represents an MCP server entry to be written into an agent's config file.
/// </summary>
public record McpServerEntry(string ServerName, string Command, string[] Args);

/// <summary>
/// Scope at which agent configuration is written.
/// </summary>
public enum AgentScope
{
    User,
    Project
}

/// <summary>
/// An agent detected on the filesystem with its config path and capabilities.
/// </summary>
public record DetectedAgent(
    string Name,
    string ConfigPath,
    AgentScope Scope,
    string WriterId,
    string? SkillDirectoryPath);
