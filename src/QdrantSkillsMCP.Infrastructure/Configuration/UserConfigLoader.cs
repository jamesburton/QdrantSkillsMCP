using Microsoft.Extensions.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Configuration;

/// <summary>
/// Loads user-level config from ~/.qdrant-skills/config.json into IConfigurationBuilder.
/// Profile-aware: reads the active profile's QdrantSkills section.
/// </summary>
public static class UserConfigLoader
{
    /// <summary>
    /// Adds user-level config from the given directory's config.json to the configuration builder.
    /// No-op if file does not exist.
    /// </summary>
    public static void AddUserConfig(IConfigurationBuilder builder, string? userDir = null)
    {
        throw new NotImplementedException("UserConfigLoader not yet implemented");
    }
}
