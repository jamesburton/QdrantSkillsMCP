using QdrantSkillsMCP.Infrastructure.Configuration;
using Xunit;

namespace QdrantSkillsMCP.UnitTests.Config;

/// <summary>
/// Tests for ConfigManager config read/write/profile/init/reset operations.
/// </summary>
public sealed class ConfigManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _userDir;
    private readonly string _projectDir;

    public ConfigManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cfg-mgr-{Guid.NewGuid():N}");
        _userDir = Path.Combine(_tempDir, "user");
        _projectDir = Path.Combine(_tempDir, "project");
        Directory.CreateDirectory(_userDir);
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void GetValue_ReturnsDefault_WhenNoConfigFile()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        var value = mgr.GetValue("QdrantHost");
        Assert.Equal("localhost", value);
    }

    [Fact]
    public void GetValue_ReturnsNull_ForUnknownKey()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        var value = mgr.GetValue("NonExistentKey");
        Assert.Null(value);
    }

    [Fact]
    public async Task SetValueAsync_WritesToUserConfig_UnderActiveProfile()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        await mgr.SetValueAsync("QdrantHost", "cloud.example.com");

        // Re-read to confirm persistence
        var mgr2 = new ConfigManager(_userDir, _projectDir);
        Assert.Equal("cloud.example.com", mgr2.GetValue("QdrantHost"));
    }

    [Fact]
    public async Task SetValueAsync_WithProjectScope_WritesToProjectConfig()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        await mgr.SetValueAsync("QdrantHost", "project-host", projectScope: true);

        var mgr2 = new ConfigManager(_userDir, _projectDir);
        Assert.Equal("project-host", mgr2.GetValue("QdrantHost"));
    }

    [Fact]
    public async Task SetValueAsync_CreatesBackup()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        await mgr.SetValueAsync("QdrantHost", "first");
        await mgr.SetValueAsync("QdrantHost", "second");

        var bakPath = Path.Combine(_userDir, "config.json.bak");
        Assert.True(File.Exists(bakPath));
    }

    [Fact]
    public void GetAllWithSources_ReturnsDefaultsWhenNoConfig()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        var all = mgr.GetAllWithSources();

        Assert.Contains("QdrantHost", all.Keys);
        Assert.Equal("[default]", all["QdrantHost"].Source);
        Assert.Equal("localhost", all["QdrantHost"].Value);
    }

    [Fact]
    public async Task GetAllWithSources_ShowsUserSource()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        await mgr.SetValueAsync("QdrantHost", "user-host");

        var mgr2 = new ConfigManager(_userDir, _projectDir);
        var all = mgr2.GetAllWithSources();

        Assert.Equal("[user]", all["QdrantHost"].Source);
        Assert.Equal("user-host", all["QdrantHost"].Value);
    }

    [Fact]
    public async Task GetAllWithSources_ProjectOverridesUser()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        await mgr.SetValueAsync("QdrantHost", "user-host");
        await mgr.SetValueAsync("QdrantHost", "project-host", projectScope: true);

        var mgr2 = new ConfigManager(_userDir, _projectDir);
        var all = mgr2.GetAllWithSources();

        Assert.Equal("[project]", all["QdrantHost"].Source);
        Assert.Equal("project-host", all["QdrantHost"].Value);
    }

    [Fact]
    public async Task GetAllWithSources_EnvOverridesAll()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        await mgr.SetValueAsync("QdrantHost", "user-host");

        Environment.SetEnvironmentVariable("QDRANT_SKILLS__QdrantHost", "env-host");
        try
        {
            var mgr2 = new ConfigManager(_userDir, _projectDir);
            var all = mgr2.GetAllWithSources();

            Assert.Equal("[env:QDRANT_SKILLS__QdrantHost]", all["QdrantHost"].Source);
            Assert.Equal("env-host", all["QdrantHost"].Value);
        }
        finally
        {
            Environment.SetEnvironmentVariable("QDRANT_SKILLS__QdrantHost", null);
        }
    }

    [Fact]
    public async Task InitAsync_CreatesConfigWithLocalProfile()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        await mgr.InitAsync();

        var configPath = Path.Combine(_userDir, "config.json");
        Assert.True(File.Exists(configPath));

        // Re-read to verify content
        var mgr2 = new ConfigManager(_userDir, _projectDir);
        Assert.Equal("localhost", mgr2.GetValue("QdrantHost"));
        Assert.Equal("6334", mgr2.GetValue("QdrantGrpcPort"));
        Assert.Equal("skills", mgr2.GetValue("CollectionName"));
    }

    [Fact]
    public async Task InitAsync_CreatesDirectoryIfNeeded()
    {
        var newDir = Path.Combine(_tempDir, "newuser");
        var mgr = new ConfigManager(newDir, _projectDir);
        await mgr.InitAsync();

        Assert.True(Directory.Exists(newDir));
        Assert.True(File.Exists(Path.Combine(newDir, "config.json")));
    }

    [Fact]
    public async Task ResetAsync_RemovesSingleKey()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        await mgr.SetValueAsync("QdrantHost", "custom");
        await mgr.ResetAsync("QdrantHost");

        var mgr2 = new ConfigManager(_userDir, _projectDir);
        // Should fall back to default
        Assert.Equal("localhost", mgr2.GetValue("QdrantHost"));
        var all = mgr2.GetAllWithSources();
        Assert.Equal("[default]", all["QdrantHost"].Source);
    }

    [Fact]
    public async Task ResetAsync_Null_RemovesAllKeys()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        await mgr.SetValueAsync("QdrantHost", "custom");
        await mgr.SetValueAsync("CollectionName", "my-skills");
        await mgr.ResetAsync(null);

        var mgr2 = new ConfigManager(_userDir, _projectDir);
        var all = mgr2.GetAllWithSources();
        Assert.Equal("[default]", all["QdrantHost"].Source);
        Assert.Equal("[default]", all["CollectionName"].Source);
    }

    [Fact]
    public async Task UseProfileAsync_SwitchesActiveProfile()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        await mgr.InitAsync();
        await mgr.UseProfileAsync("cloud");

        var mgr2 = new ConfigManager(_userDir, _projectDir);
        var profiles = mgr2.GetProfiles();
        Assert.Contains("cloud", profiles);
    }

    [Fact]
    public async Task GetProfiles_ReturnsProfileNames()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        await mgr.InitAsync();

        var profiles = mgr.GetProfiles();
        Assert.Contains("local", profiles);
    }

    [Fact]
    public void ReadingNonExistentConfig_ReturnsDefaults()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        var all = mgr.GetAllWithSources();

        // All keys should be present with default source
        Assert.All(all.Values, entry => Assert.Equal("[default]", entry.Source));
    }

    [Fact]
    public async Task ProjectConfig_HasNoProfiles()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        await mgr.SetValueAsync("QdrantHost", "proj-host", projectScope: true);

        var projectConfigPath = Path.Combine(_projectDir, "qdrant-skills.json");
        var json = await File.ReadAllTextAsync(projectConfigPath);

        // Should have flat QdrantSkills section, no profiles
        Assert.Contains("QdrantSkills", json);
        Assert.DoesNotContain("profiles", json);
        Assert.DoesNotContain("activeProfile", json);
    }

    [Fact]
    public void ConfigurableKeys_ExcludesInternalProperties()
    {
        var keys = ConfigManager.ConfigurableKeys;

        Assert.DoesNotContain("TestEmbeddingKey", keys);
        Assert.DoesNotContain("TestEmbeddingInput", keys);
        Assert.DoesNotContain("SkipEmbeddingOutputValidation", keys);
        Assert.DoesNotContain("MismatchResolution", keys);

        Assert.Contains("QdrantHost", keys);
        Assert.Contains("QdrantApiKey", keys);
        Assert.Contains("OpenAiApiKey", keys);
    }
}
