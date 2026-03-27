using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Configuration;

/// <summary>
/// Loads user-level config from ~/.qdrant-skills/config.json into IConfigurationBuilder.
/// Profile-aware: reads the active profile's QdrantSkills section and adds it as in-memory collection.
/// </summary>
public static class UserConfigLoader
{
    /// <summary>
    /// Adds user-level config from the given directory's config.json to the configuration builder.
    /// No-op if file does not exist. Reads the active profile's QdrantSkills section.
    /// </summary>
    public static void AddUserConfig(IConfigurationBuilder builder, string? userDir = null)
    {
        var dir = userDir
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qdrant-skills");

        var configPath = Path.Combine(dir, "config.json");
        if (!File.Exists(configPath))
            return;

        string json;
        try
        {
            json = File.ReadAllText(configPath);
        }
        catch
        {
            return; // File unreadable -- no-op
        }

        var root = JsonNode.Parse(json);
        if (root is null)
            return;

        var activeProfile = root["activeProfile"]?.GetValue<string>() ?? "local";
        var section = root["profiles"]?[activeProfile]?[QdrantSkillsOptions.SectionName] as JsonObject;

        if (section is null)
            return;

        // Flatten the profile's QdrantSkills section into key-value pairs for IConfiguration
        var kvPairs = new Dictionary<string, string?>();
        foreach (var prop in section)
        {
            if (prop.Value is not null)
            {
                kvPairs[$"{QdrantSkillsOptions.SectionName}:{prop.Key}"] = prop.Value.ToString();
            }
        }

        if (kvPairs.Count > 0)
        {
            builder.AddInMemoryCollection(kvPairs);
        }
    }
}
