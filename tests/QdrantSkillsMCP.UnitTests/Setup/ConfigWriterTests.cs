using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using QdrantSkillsMCP.Infrastructure.Setup;
using QdrantSkillsMCP.Infrastructure.Setup.Writers;

namespace QdrantSkillsMCP.UnitTests.Setup;

public sealed class ConfigWriterTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "qdrant-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static McpServerEntry DefaultEntry => new("qdrant-skills-mcp", "dnx", ["qdrant-skills-mcp"]);

    #region ClaudeConfigWriter

    [Fact]
    public async Task ClaudeWriter_WritesToNewFile_HasMcpServersKey()
    {
        // Arrange
        var writer = new ClaudeConfigWriter();
        var configPath = Path.Combine(_tempDir, ".claude.json");

        // Act
        await writer.WriteConfigAsync(configPath, DefaultEntry);

        // Assert
        var json = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        Assert.NotNull(json["mcpServers"]);
        Assert.NotNull(json["mcpServers"]!["qdrant-skills-mcp"]);
        Assert.Equal("dnx", json["mcpServers"]!["qdrant-skills-mcp"]!["command"]!.GetValue<string>());
    }

    [Fact]
    public async Task ClaudeWriter_MergesWithExistingConfig_PreservesOtherServers()
    {
        // Arrange
        var writer = new ClaudeConfigWriter();
        var configPath = Path.Combine(_tempDir, ".claude.json");
        var existing = new JsonObject
        {
            ["mcpServers"] = new JsonObject
            {
                ["other-server"] = new JsonObject
                {
                    ["command"] = "other-cmd",
                    ["args"] = new JsonArray("arg1")
                }
            }
        };
        await File.WriteAllTextAsync(configPath, existing.ToJsonString());

        // Act
        await writer.WriteConfigAsync(configPath, DefaultEntry);

        // Assert
        var json = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        Assert.NotNull(json["mcpServers"]!["other-server"]);
        Assert.Equal("other-cmd", json["mcpServers"]!["other-server"]!["command"]!.GetValue<string>());
        Assert.NotNull(json["mcpServers"]!["qdrant-skills-mcp"]);
    }

    [Fact]
    public async Task ClaudeWriter_CreatesBackupBeforeWrite()
    {
        // Arrange
        var writer = new ClaudeConfigWriter();
        var configPath = Path.Combine(_tempDir, ".claude.json");
        await File.WriteAllTextAsync(configPath, """{"existing": true}""");

        // Act
        await writer.WriteConfigAsync(configPath, DefaultEntry);

        // Assert
        Assert.True(File.Exists(configPath + ".bak"));
        var backup = await File.ReadAllTextAsync(configPath + ".bak");
        Assert.Contains("existing", backup);
    }

    [Fact]
    public void ClaudeWriter_SkillDirectoryPath_EndsWithExpectedPath()
    {
        var writer = new ClaudeConfigWriter();
        Assert.NotNull(writer.SkillDirectoryPath);
        Assert.EndsWith(
            Path.Combine(".claude", "skills", "qdrant-skills-mcp"),
            writer.SkillDirectoryPath!);
    }

    [Fact]
    public async Task ClaudeWriter_ProducesValidJson()
    {
        var writer = new ClaudeConfigWriter();
        var configPath = Path.Combine(_tempDir, ".claude.json");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        // Should not throw
        var text = await File.ReadAllTextAsync(configPath);
        var node = JsonNode.Parse(text);
        Assert.NotNull(node);
    }

    [Fact]
    public async Task ClaudeWriter_NewFile_CreatesDirectoryIfNeeded()
    {
        var writer = new ClaudeConfigWriter();
        var configPath = Path.Combine(_tempDir, "subdir", ".claude.json");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        Assert.True(File.Exists(configPath));
    }

    #endregion

    #region CopilotConfigWriter

    [Fact]
    public async Task CopilotWriter_UsesServersRootKey()
    {
        // Arrange
        var writer = new CopilotConfigWriter();
        var configPath = Path.Combine(_tempDir, "mcp.json");

        // Act
        await writer.WriteConfigAsync(configPath, DefaultEntry);

        // Assert
        var json = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        Assert.NotNull(json["servers"]);
        Assert.Null(json["mcpServers"]); // Must NOT use mcpServers
        Assert.NotNull(json["servers"]!["qdrant-skills-mcp"]);
        Assert.Equal("stdio", json["servers"]!["qdrant-skills-mcp"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void CopilotWriter_SkillDirectoryPath_IsNull()
    {
        var writer = new CopilotConfigWriter();
        Assert.Null(writer.SkillDirectoryPath);
    }

    #endregion

    #region ClaudeDesktopConfigWriter

    [Fact]
    public async Task ClaudeDesktopWriter_UsesMcpServersKey()
    {
        var writer = new ClaudeDesktopConfigWriter();
        var configPath = Path.Combine(_tempDir, "claude_desktop_config.json");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        var json = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        Assert.NotNull(json["mcpServers"]!["qdrant-skills-mcp"]);
    }

    [Fact]
    public void ClaudeDesktopWriter_SkillDirectoryPath_IsNull()
    {
        var writer = new ClaudeDesktopConfigWriter();
        Assert.Null(writer.SkillDirectoryPath);
    }

    [Fact]
    public void ClaudeDesktopWriter_OnlySupportsUserScope()
    {
        var writer = new ClaudeDesktopConfigWriter();
        Assert.Single(writer.SupportedScopes);
        Assert.Equal(AgentScope.User, writer.SupportedScopes[0]);
    }

    #endregion

    #region CopilotCliConfigWriter

    [Fact]
    public async Task CopilotCliWriter_UsesMcpServersWithTypeLocal()
    {
        var writer = new CopilotCliConfigWriter();
        var configPath = Path.Combine(_tempDir, "mcp-config.json");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        var json = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        Assert.NotNull(json["mcpServers"]!["qdrant-skills-mcp"]);
        Assert.Equal("local", json["mcpServers"]!["qdrant-skills-mcp"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void CopilotCliWriter_OnlySupportsUserScope()
    {
        var writer = new CopilotCliConfigWriter();
        Assert.Single(writer.SupportedScopes);
        Assert.Equal(AgentScope.User, writer.SupportedScopes[0]);
    }

    #endregion

    #region GenerateSnippet

    [Fact]
    public void ClaudeWriter_GenerateSnippet_ContainsValidJson()
    {
        var writer = new ClaudeConfigWriter();
        var snippet = writer.GenerateSnippet(DefaultEntry, AgentScope.User);

        // Should be valid JSON
        var json = JsonNode.Parse(snippet);
        Assert.NotNull(json);
        Assert.NotNull(json!["mcpServers"]!["qdrant-skills-mcp"]);
    }

    #endregion
}
