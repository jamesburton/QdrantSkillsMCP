using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QdrantSkillsMCP.Infrastructure.Configuration;

/// <summary>
/// Manages configuration read/write/profile operations against user-level and project-level JSON files.
/// User config: ~/.qdrant-skills/config.json (with profiles).
/// Project config: {projectDir}/qdrant-skills.json (flat QdrantSkills section, no profiles).
/// </summary>
public sealed class ConfigManager
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// User-configurable property names from <see cref="QdrantSkillsOptions"/>.
    /// Excludes internal/test properties.
    /// </summary>
    public static readonly IReadOnlyList<string> ConfigurableKeys = GetConfigurableKeys();

    private static readonly Dictionary<string, string> Defaults = GetDefaults();

    private readonly string _userDir;
    private readonly string _projectDir;

    /// <summary>
    /// Creates a new ConfigManager.
    /// </summary>
    /// <param name="userDir">User-level config directory. Defaults to ~/.qdrant-skills/.</param>
    /// <param name="projectDir">Project-level directory. Defaults to current directory.</param>
    public ConfigManager(string? userDir = null, string? projectDir = null)
    {
        _userDir = userDir
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qdrant-skills");
        _projectDir = projectDir ?? Directory.GetCurrentDirectory();
    }

    private string UserConfigPath => Path.Combine(_userDir, "config.json");
    private string ProjectConfigPath => Path.Combine(_projectDir, "qdrant-skills.json");

    /// <summary>
    /// Gets the resolved value for a configurable key using precedence: env > project > user > default.
    /// Returns null for unknown keys.
    /// </summary>
    public string? GetValue(string key)
    {
        if (!ConfigurableKeys.Contains(key))
            return null;

        // Check env var first
        var envValue = Environment.GetEnvironmentVariable($"QDRANT_SKILLS__{key}");
        if (envValue is not null)
            return envValue;

        // Check project config
        var projectValue = ReadProjectValue(key);
        if (projectValue is not null)
            return projectValue;

        // Check user config (active profile)
        var userValue = ReadUserValue(key);
        if (userValue is not null)
            return userValue;

        // Return default
        return Defaults.GetValueOrDefault(key);
    }

    /// <summary>
    /// Sets a config value. Writes to user config (active profile) or project config.
    /// Creates a .bak backup before writing.
    /// </summary>
    public async Task SetValueAsync(string key, string value, bool projectScope = false)
    {
        if (projectScope)
        {
            await WriteProjectValueAsync(key, value);
        }
        else
        {
            await WriteUserValueAsync(key, value);
        }
    }

    /// <summary>
    /// Returns all configurable keys with their resolved values and source annotations.
    /// Source precedence: env > project > user > default.
    /// </summary>
    public Dictionary<string, ConfigEntry> GetAllWithSources()
    {
        var userValues = ReadAllUserValues();
        var projectValues = ReadAllProjectValues();
        var result = new Dictionary<string, ConfigEntry>();

        foreach (var key in ConfigurableKeys)
        {
            // Check env
            var envValue = Environment.GetEnvironmentVariable($"QDRANT_SKILLS__{key}");
            if (envValue is not null)
            {
                result[key] = new ConfigEntry(envValue, $"[env:QDRANT_SKILLS__{key}]");
                continue;
            }

            // Check project
            if (projectValues.TryGetValue(key, out var projVal))
            {
                result[key] = new ConfigEntry(projVal, "[project]");
                continue;
            }

            // Check user
            if (userValues.TryGetValue(key, out var userVal))
            {
                result[key] = new ConfigEntry(userVal, "[user]");
                continue;
            }

            // Default
            var defaultVal = Defaults.GetValueOrDefault(key);
            result[key] = new ConfigEntry(defaultVal, "[default]");
        }

        return result;
    }

    /// <summary>
    /// Initializes config with a "local" profile preset.
    /// Creates the user directory and config.json if they don't exist.
    /// </summary>
    public async Task InitAsync()
    {
        Directory.CreateDirectory(_userDir);

        var root = new JsonObject
        {
            ["activeProfile"] = "local",
            ["profiles"] = new JsonObject
            {
                ["local"] = new JsonObject
                {
                    [QdrantSkillsOptions.SectionName] = new JsonObject
                    {
                        ["QdrantHost"] = "localhost",
                        ["QdrantGrpcPort"] = 6334,
                        ["CollectionName"] = "skills",
                        ["EmbeddingProvider"] = "LocalONNX"
                    }
                }
            }
        };

        await File.WriteAllTextAsync(UserConfigPath, root.ToJsonString(WriteOptions));
    }

    /// <summary>
    /// Resets a specific key or all keys in the active profile.
    /// Pass null to reset all keys.
    /// </summary>
    public async Task ResetAsync(string? key)
    {
        if (!File.Exists(UserConfigPath))
            return;

        var json = await File.ReadAllTextAsync(UserConfigPath);
        var root = JsonNode.Parse(json) as JsonObject;
        if (root is null) return;

        var activeProfile = root["activeProfile"]?.GetValue<string>() ?? "local";
        var profileSection = root["profiles"]?[activeProfile]?[QdrantSkillsOptions.SectionName] as JsonObject;

        if (profileSection is null) return;

        if (key is null)
        {
            // Remove all keys
            profileSection.Clear();
        }
        else
        {
            profileSection.Remove(key);
        }

        BackupFile(UserConfigPath);
        await File.WriteAllTextAsync(UserConfigPath, root.ToJsonString(WriteOptions));
    }

    /// <summary>
    /// Sets the active profile. Creates the profile if it doesn't exist.
    /// </summary>
    public async Task UseProfileAsync(string profileName)
    {
        JsonObject root;
        if (File.Exists(UserConfigPath))
        {
            var json = await File.ReadAllTextAsync(UserConfigPath);
            root = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        else
        {
            Directory.CreateDirectory(_userDir);
            root = new JsonObject();
        }

        root["activeProfile"] = profileName;

        // Ensure profiles object and this profile exist
        if (root["profiles"] is not JsonObject)
            root["profiles"] = new JsonObject();

        if (root["profiles"]![profileName] is null)
        {
            root["profiles"]![profileName] = new JsonObject
            {
                [QdrantSkillsOptions.SectionName] = new JsonObject()
            };
        }

        BackupFile(UserConfigPath);
        await File.WriteAllTextAsync(UserConfigPath, root.ToJsonString(WriteOptions));
    }

    /// <summary>
    /// Returns the list of profile names from user config.
    /// </summary>
    public IReadOnlyList<string> GetProfiles()
    {
        if (!File.Exists(UserConfigPath))
            return [];

        var json = File.ReadAllText(UserConfigPath);
        var root = JsonNode.Parse(json);
        var profiles = root?["profiles"] as JsonObject;

        if (profiles is null)
            return [];

        return profiles.Select(p => p.Key).ToList().AsReadOnly();
    }

    #region Private helpers

    private string? ReadUserValue(string key)
    {
        if (!File.Exists(UserConfigPath))
            return null;

        var json = File.ReadAllText(UserConfigPath);
        var root = JsonNode.Parse(json);
        var activeProfile = root?["activeProfile"]?.GetValue<string>() ?? "local";
        var value = root?["profiles"]?[activeProfile]?[QdrantSkillsOptions.SectionName]?[key];

        return value?.ToString();
    }

    private string? ReadProjectValue(string key)
    {
        if (!File.Exists(ProjectConfigPath))
            return null;

        var json = File.ReadAllText(ProjectConfigPath);
        var root = JsonNode.Parse(json);
        return root?[QdrantSkillsOptions.SectionName]?[key]?.ToString();
    }

    private Dictionary<string, string> ReadAllUserValues()
    {
        var result = new Dictionary<string, string>();
        if (!File.Exists(UserConfigPath))
            return result;

        var json = File.ReadAllText(UserConfigPath);
        var root = JsonNode.Parse(json);
        var activeProfile = root?["activeProfile"]?.GetValue<string>() ?? "local";
        var section = root?["profiles"]?[activeProfile]?[QdrantSkillsOptions.SectionName] as JsonObject;

        if (section is null) return result;

        foreach (var prop in section)
        {
            if (prop.Value is not null)
                result[prop.Key] = prop.Value.ToString();
        }

        return result;
    }

    private Dictionary<string, string> ReadAllProjectValues()
    {
        var result = new Dictionary<string, string>();
        if (!File.Exists(ProjectConfigPath))
            return result;

        var json = File.ReadAllText(ProjectConfigPath);
        var root = JsonNode.Parse(json);
        var section = root?[QdrantSkillsOptions.SectionName] as JsonObject;

        if (section is null) return result;

        foreach (var prop in section)
        {
            if (prop.Value is not null)
                result[prop.Key] = prop.Value.ToString();
        }

        return result;
    }

    private async Task WriteUserValueAsync(string key, string value)
    {
        JsonObject root;
        if (File.Exists(UserConfigPath))
        {
            BackupFile(UserConfigPath);
            var json = await File.ReadAllTextAsync(UserConfigPath);
            root = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        else
        {
            Directory.CreateDirectory(_userDir);
            root = new JsonObject();
        }

        var activeProfile = root["activeProfile"]?.GetValue<string>() ?? "local";

        // Ensure profile structure exists
        if (root["profiles"] is not JsonObject)
        {
            root["activeProfile"] = activeProfile;
            root["profiles"] = new JsonObject();
        }

        if (root["profiles"]![activeProfile] is null)
        {
            root["profiles"]![activeProfile] = new JsonObject
            {
                [QdrantSkillsOptions.SectionName] = new JsonObject()
            };
        }

        if (root["profiles"]![activeProfile]![QdrantSkillsOptions.SectionName] is not JsonObject)
        {
            root["profiles"]![activeProfile]![QdrantSkillsOptions.SectionName] = new JsonObject();
        }

        root["profiles"]![activeProfile]![QdrantSkillsOptions.SectionName]![key] = value;

        await File.WriteAllTextAsync(UserConfigPath, root.ToJsonString(WriteOptions));
    }

    private async Task WriteProjectValueAsync(string key, string value)
    {
        JsonObject root;
        if (File.Exists(ProjectConfigPath))
        {
            BackupFile(ProjectConfigPath);
            var json = await File.ReadAllTextAsync(ProjectConfigPath);
            root = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        if (root[QdrantSkillsOptions.SectionName] is not JsonObject)
        {
            root[QdrantSkillsOptions.SectionName] = new JsonObject();
        }

        root[QdrantSkillsOptions.SectionName]![key] = value;

        await File.WriteAllTextAsync(ProjectConfigPath, root.ToJsonString(WriteOptions));
    }

    private static void BackupFile(string path)
    {
        if (File.Exists(path))
            File.Copy(path, path + ".bak", overwrite: true);
    }

    private static IReadOnlyList<string> GetConfigurableKeys()
    {
        var excluded = new HashSet<string>
        {
            nameof(QdrantSkillsOptions.TestEmbeddingKey),
            nameof(QdrantSkillsOptions.TestEmbeddingInput),
            nameof(QdrantSkillsOptions.SkipEmbeddingOutputValidation),
            nameof(QdrantSkillsOptions.MismatchResolution)
        };

        return typeof(QdrantSkillsOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !excluded.Contains(p.Name))
            .Select(p => p.Name)
            .ToList()
            .AsReadOnly();
    }

    private static Dictionary<string, string> GetDefaults()
    {
        var opts = new QdrantSkillsOptions();
        var result = new Dictionary<string, string>();

        foreach (var key in GetConfigurableKeys())
        {
            var prop = typeof(QdrantSkillsOptions).GetProperty(key);
            var value = prop?.GetValue(opts);
            if (value is not null)
                result[key] = value.ToString()!;
        }

        return result;
    }

    #endregion
}

/// <summary>
/// A config entry with its resolved value and source annotation.
/// </summary>
/// <param name="Value">The resolved value (null if not set).</param>
/// <param name="Source">Source annotation: [default], [user], [project], or [env:QDRANT_SKILLS__*].</param>
public sealed record ConfigEntry(string? Value, string Source);
