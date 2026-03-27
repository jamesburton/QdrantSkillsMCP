using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using QdrantSkillsMCP.Infrastructure.Configuration;
using Xunit;

namespace QdrantSkillsMCP.UnitTests.Config;

/// <summary>
/// Tests for AddUserConfig integration that loads user-level config into IConfiguration.
/// </summary>
public sealed class UserConfigWiringTests : IDisposable
{
    private readonly string _tempDir;

    public UserConfigWiringTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"usr-cfg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void AddUserConfig_WithValidFile_PopulatesConfiguration()
    {
        // Arrange: create a user config with a profile
        var configPath = Path.Combine(_tempDir, "config.json");
        var configJson = new JsonObject
        {
            ["activeProfile"] = "local",
            ["profiles"] = new JsonObject
            {
                ["local"] = new JsonObject
                {
                    ["QdrantSkills"] = new JsonObject
                    {
                        ["QdrantHost"] = "my-qdrant-server.com",
                        ["QdrantGrpcPort"] = 6335
                    }
                }
            }
        };
        File.WriteAllText(configPath, configJson.ToJsonString());

        // Act
        var builder = new ConfigurationBuilder();
        UserConfigLoader.AddUserConfig(builder, _tempDir);
        var config = builder.Build();

        // Assert
        Assert.Equal("my-qdrant-server.com", config["QdrantSkills:QdrantHost"]);
        Assert.Equal("6335", config["QdrantSkills:QdrantGrpcPort"]);
    }

    [Fact]
    public void AddUserConfig_WithMissingFile_DoesNotThrow()
    {
        var emptyDir = Path.Combine(_tempDir, "nonexistent");
        Directory.CreateDirectory(emptyDir);

        var builder = new ConfigurationBuilder();
        UserConfigLoader.AddUserConfig(builder, emptyDir);
        var config = builder.Build();

        // Should not throw, and config should be empty/null for QdrantSkills keys
        Assert.Null(config["QdrantSkills:QdrantHost"]);
    }

    [Fact]
    public void AddUserConfig_RespectsActiveProfile()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var configJson = new JsonObject
        {
            ["activeProfile"] = "cloud",
            ["profiles"] = new JsonObject
            {
                ["local"] = new JsonObject
                {
                    ["QdrantSkills"] = new JsonObject
                    {
                        ["QdrantHost"] = "localhost"
                    }
                },
                ["cloud"] = new JsonObject
                {
                    ["QdrantSkills"] = new JsonObject
                    {
                        ["QdrantHost"] = "cloud.qdrant.io"
                    }
                }
            }
        };
        File.WriteAllText(configPath, configJson.ToJsonString());

        var builder = new ConfigurationBuilder();
        UserConfigLoader.AddUserConfig(builder, _tempDir);
        var config = builder.Build();

        Assert.Equal("cloud.qdrant.io", config["QdrantSkills:QdrantHost"]);
    }

    [Fact]
    public void UserConfig_IsOverriddenByProjectConfig()
    {
        // User config
        var configPath = Path.Combine(_tempDir, "config.json");
        var userJson = new JsonObject
        {
            ["activeProfile"] = "local",
            ["profiles"] = new JsonObject
            {
                ["local"] = new JsonObject
                {
                    ["QdrantSkills"] = new JsonObject
                    {
                        ["QdrantHost"] = "user-host"
                    }
                }
            }
        };
        File.WriteAllText(configPath, userJson.ToJsonString());

        // Project config (added after user config = higher precedence)
        var projectJson = new JsonObject
        {
            ["QdrantSkills"] = new JsonObject
            {
                ["QdrantHost"] = "project-host"
            }
        };
        var projectPath = Path.Combine(_tempDir, "project-config.json");
        File.WriteAllText(projectPath, projectJson.ToJsonString());

        var builder = new ConfigurationBuilder();
        UserConfigLoader.AddUserConfig(builder, _tempDir);
        builder.AddJsonFile(projectPath, optional: true);
        var config = builder.Build();

        // Project config overrides user config (added later = higher precedence)
        Assert.Equal("project-host", config["QdrantSkills:QdrantHost"]);
    }
}
