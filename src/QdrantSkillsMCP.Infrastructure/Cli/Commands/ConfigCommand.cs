using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Cli.Commands;

/// <summary>
/// CLI command dispatcher for all --config operations.
/// </summary>
public static class ConfigCommand
{
    /// <summary>
    /// Dispatches to the appropriate config subcommand based on args.
    /// </summary>
    public static Task<int> RunAsync(ConfigManager configManager, string[] args)
    {
        throw new NotImplementedException("ConfigCommand not yet implemented");
    }
}
