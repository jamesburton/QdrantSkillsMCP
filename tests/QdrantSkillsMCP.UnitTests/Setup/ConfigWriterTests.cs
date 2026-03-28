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

    #region CodexConfigWriter

    [Fact]
    public async Task CodexWriter_WritesTomlFormat()
    {
        var writer = new CodexConfigWriter();
        var configPath = Path.Combine(_tempDir, "config.toml");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        var toml = await File.ReadAllTextAsync(configPath);
        Assert.Contains("[mcp_servers.qdrant-skills-mcp]", toml);
        Assert.Contains("command", toml);
        Assert.Contains("dnx", toml);
    }

    [Fact]
    public async Task CodexWriter_MergesWithExistingToml()
    {
        var writer = new CodexConfigWriter();
        var configPath = Path.Combine(_tempDir, "config.toml");
        await File.WriteAllTextAsync(configPath, """
            [mcp_servers.other-server]
            command = "other-cmd"
            args = ["arg1"]
            """);

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        var toml = await File.ReadAllTextAsync(configPath);
        Assert.Contains("other-server", toml);
        Assert.Contains("qdrant-skills-mcp", toml);
    }

    [Fact]
    public async Task CodexWriter_CreatesBackup()
    {
        var writer = new CodexConfigWriter();
        var configPath = Path.Combine(_tempDir, "config.toml");
        await File.WriteAllTextAsync(configPath, "[existing]\nkey = \"value\"\n");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        Assert.True(File.Exists(configPath + ".bak"));
    }

    [Fact]
    public void CodexWriter_SkillDirectoryPath_IsNull()
    {
        var writer = new CodexConfigWriter();
        Assert.Null(writer.SkillDirectoryPath);
    }

    #endregion

    #region OpenCodeConfigWriter

    [Fact]
    public async Task OpenCodeWriter_UsesMcpRootKeyWithCommandArray()
    {
        var writer = new OpenCodeConfigWriter();
        var configPath = Path.Combine(_tempDir, "opencode.json");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        var json = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        Assert.NotNull(json["mcp"]);
        Assert.NotNull(json["mcp"]!["qdrant-skills-mcp"]);

        // command should be an array ["dnx", "qdrant-skills-mcp"]
        var command = json["mcp"]!["qdrant-skills-mcp"]!["command"]!.AsArray();
        Assert.Equal(2, command.Count);
        Assert.Equal("dnx", command[0]!.GetValue<string>());
        Assert.Equal("qdrant-skills-mcp", command[1]!.GetValue<string>());
        Assert.Equal("local", json["mcp"]!["qdrant-skills-mcp"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void OpenCodeWriter_SkillDirectoryPath_IsNull()
    {
        var writer = new OpenCodeConfigWriter();
        Assert.Null(writer.SkillDirectoryPath);
    }

    #endregion

    #region KiloCodeConfigWriter

    [Fact]
    public async Task KiloCodeWriter_UsesMcpServersKey()
    {
        var writer = new KiloCodeConfigWriter();
        var configPath = Path.Combine(_tempDir, "mcp_settings.json");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        var json = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        Assert.NotNull(json["mcpServers"]!["qdrant-skills-mcp"]);
        Assert.Equal("dnx", json["mcpServers"]!["qdrant-skills-mcp"]!["command"]!.GetValue<string>());
    }

    [Fact]
    public void KiloCodeWriter_SkillDirectoryPath_IsNull()
    {
        var writer = new KiloCodeConfigWriter();
        Assert.Null(writer.SkillDirectoryPath);
    }

    #endregion

    #region FactoryDroidConfigWriter

    [Fact]
    public async Task FactoryDroidWriter_HasTypeStdio()
    {
        var writer = new FactoryDroidConfigWriter();
        var configPath = Path.Combine(_tempDir, "mcp.json");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        var json = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        Assert.NotNull(json["mcpServers"]!["qdrant-skills-mcp"]);
        Assert.Equal("stdio", json["mcpServers"]!["qdrant-skills-mcp"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void FactoryDroidWriter_SkillDirectoryPath_IsNull()
    {
        var writer = new FactoryDroidConfigWriter();
        Assert.Null(writer.SkillDirectoryPath);
    }

    #endregion

    #region CursorConfigWriter

    [Fact]
    public async Task CursorWriter_UsesMcpServersKey()
    {
        var writer = new CursorConfigWriter();
        var configPath = Path.Combine(_tempDir, "mcp.json");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        var json = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        Assert.NotNull(json["mcpServers"]);
        Assert.NotNull(json["mcpServers"]!["qdrant-skills-mcp"]);
        Assert.Equal("dnx", json["mcpServers"]!["qdrant-skills-mcp"]!["command"]!.GetValue<string>());
    }

    [Fact]
    public async Task CursorWriter_CreatesBackup()
    {
        var writer = new CursorConfigWriter();
        var configPath = Path.Combine(_tempDir, "mcp.json");
        await File.WriteAllTextAsync(configPath, """{"existing": true}""");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        Assert.True(File.Exists(configPath + ".bak"));
        var backup = await File.ReadAllTextAsync(configPath + ".bak");
        Assert.Contains("existing", backup);
    }

    [Fact]
    public void CursorWriter_OnlySupportsProjectScope()
    {
        var writer = new CursorConfigWriter();
        Assert.Single(writer.SupportedScopes);
        Assert.Equal(AgentScope.Project, writer.SupportedScopes[0]);
    }

    [Fact]
    public void CursorWriter_SkillDirectoryPath_IsNull()
    {
        var writer = new CursorConfigWriter();
        Assert.Null(writer.SkillDirectoryPath);
    }

    [Fact]
    public void CursorWriter_GenerateSnippet_ContainsMcpServersKey()
    {
        var writer = new CursorConfigWriter();
        var snippet = writer.GenerateSnippet(DefaultEntry, AgentScope.Project);

        var json = JsonNode.Parse(snippet);
        Assert.NotNull(json);
        Assert.NotNull(json!["mcpServers"]!["qdrant-skills-mcp"]);
    }

    #endregion

    #region WindsurfConfigWriter

    [Fact]
    public async Task WindsurfWriter_UsesMcpServersKey()
    {
        var writer = new WindsurfConfigWriter();
        var configPath = Path.Combine(_tempDir, "mcp_config.json");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        var json = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        Assert.NotNull(json["mcpServers"]);
        Assert.NotNull(json["mcpServers"]!["qdrant-skills-mcp"]);
        Assert.Equal("dnx", json["mcpServers"]!["qdrant-skills-mcp"]!["command"]!.GetValue<string>());
    }

    [Fact]
    public async Task WindsurfWriter_CreatesBackup()
    {
        var writer = new WindsurfConfigWriter();
        var configPath = Path.Combine(_tempDir, "mcp_config.json");
        await File.WriteAllTextAsync(configPath, """{"existing": true}""");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        Assert.True(File.Exists(configPath + ".bak"));
        var backup = await File.ReadAllTextAsync(configPath + ".bak");
        Assert.Contains("existing", backup);
    }

    [Fact]
    public void WindsurfWriter_OnlySupportsUserScope()
    {
        var writer = new WindsurfConfigWriter();
        Assert.Single(writer.SupportedScopes);
        Assert.Equal(AgentScope.User, writer.SupportedScopes[0]);
    }

    [Fact]
    public void WindsurfWriter_SkillDirectoryPath_IsNull()
    {
        var writer = new WindsurfConfigWriter();
        Assert.Null(writer.SkillDirectoryPath);
    }

    [Fact]
    public void WindsurfWriter_GenerateSnippet_ContainsMcpServersKey()
    {
        var writer = new WindsurfConfigWriter();
        var snippet = writer.GenerateSnippet(DefaultEntry, AgentScope.User);

        var json = JsonNode.Parse(snippet);
        Assert.NotNull(json);
        Assert.NotNull(json!["mcpServers"]!["qdrant-skills-mcp"]);
    }

    #endregion

    #region ZedConfigWriter

    [Fact]
    public async Task ZedWriter_UsesNestedContextServersFormat()
    {
        var writer = new ZedConfigWriter();
        var configPath = Path.Combine(_tempDir, "settings.json");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        var json = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        Assert.NotNull(json["assistant"]);
        Assert.NotNull(json["assistant"]!["context_servers"]);
        Assert.NotNull(json["assistant"]!["context_servers"]!["qdrant-skills-mcp"]);
        Assert.Null(json["mcpServers"]); // Must NOT use mcpServers at root
        Assert.Equal("dnx", json["assistant"]!["context_servers"]!["qdrant-skills-mcp"]!["command"]!["path"]!.GetValue<string>());
    }

    [Fact]
    public async Task ZedWriter_CommandHasPathAndArgs()
    {
        var writer = new ZedConfigWriter();
        var configPath = Path.Combine(_tempDir, "settings.json");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        var json = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        var command = json["assistant"]!["context_servers"]!["qdrant-skills-mcp"]!["command"]!;
        Assert.Equal("dnx", command["path"]!.GetValue<string>());
        var args = command["args"]!.AsArray();
        Assert.Single(args);
        Assert.Equal("qdrant-skills-mcp", args[0]!.GetValue<string>());
    }

    [Fact]
    public async Task ZedWriter_MergesWithExistingConfig_PreservesOtherKeys()
    {
        var writer = new ZedConfigWriter();
        var configPath = Path.Combine(_tempDir, "settings.json");
        var existing = new JsonObject
        {
            ["theme"] = "dark",
            ["assistant"] = new JsonObject
            {
                ["context_servers"] = new JsonObject
                {
                    ["other-server"] = new JsonObject
                    {
                        ["command"] = new JsonObject
                        {
                            ["path"] = "other-cmd",
                            ["args"] = new JsonArray("arg1")
                        }
                    }
                }
            }
        };
        await File.WriteAllTextAsync(configPath, existing.ToJsonString());

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        var json = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!;
        Assert.Equal("dark", json["theme"]!.GetValue<string>());
        Assert.NotNull(json["assistant"]!["context_servers"]!["other-server"]);
        Assert.NotNull(json["assistant"]!["context_servers"]!["qdrant-skills-mcp"]);
    }

    [Fact]
    public async Task ZedWriter_CreatesBackup()
    {
        var writer = new ZedConfigWriter();
        var configPath = Path.Combine(_tempDir, "settings.json");
        await File.WriteAllTextAsync(configPath, """{"existing": true}""");

        await writer.WriteConfigAsync(configPath, DefaultEntry);

        Assert.True(File.Exists(configPath + ".bak"));
        var backup = await File.ReadAllTextAsync(configPath + ".bak");
        Assert.Contains("existing", backup);
    }

    [Fact]
    public void ZedWriter_OnlySupportsUserScope()
    {
        var writer = new ZedConfigWriter();
        Assert.Single(writer.SupportedScopes);
        Assert.Equal(AgentScope.User, writer.SupportedScopes[0]);
    }

    [Fact]
    public void ZedWriter_SkillDirectoryPath_IsNull()
    {
        var writer = new ZedConfigWriter();
        Assert.Null(writer.SkillDirectoryPath);
    }

    [Fact]
    public void ZedWriter_GenerateSnippet_HasNestedStructure()
    {
        var writer = new ZedConfigWriter();
        var snippet = writer.GenerateSnippet(DefaultEntry, AgentScope.User);

        var json = JsonNode.Parse(snippet);
        Assert.NotNull(json);
        Assert.NotNull(json!["assistant"]!["context_servers"]!["qdrant-skills-mcp"]);
        Assert.Equal("dnx", json["assistant"]!["context_servers"]!["qdrant-skills-mcp"]!["command"]!["path"]!.GetValue<string>());
    }

    #endregion

    #region SnippetFallbackWriter

    [Fact]
    public void SnippetFallback_CanAutoWrite_IsFalse()
    {
        var writer = new SnippetFallbackWriter();
        Assert.False(writer.CanAutoWrite);
    }

    [Fact]
    public void SnippetFallback_GenerateSnippet_ContainsJsonAndInstructions()
    {
        var writer = new SnippetFallbackWriter();
        var snippet = writer.GenerateSnippet(DefaultEntry, AgentScope.User);

        Assert.Contains("qdrant-skills-mcp", snippet);
        Assert.Contains("dnx", snippet);
        Assert.Contains("mcpServers", snippet);
        Assert.Contains("Add the following", snippet);
    }

    [Fact]
    public void SnippetFallback_DetectInstallation_AlwaysReturnsNull()
    {
        var writer = new SnippetFallbackWriter();
        Assert.Null(writer.DetectInstallation(AgentScope.User));
        Assert.Null(writer.DetectInstallation(AgentScope.Project));
    }

    [Fact]
    public void SnippetFallback_SkillDirectoryPath_IsNull()
    {
        var writer = new SnippetFallbackWriter();
        Assert.Null(writer.SkillDirectoryPath);
    }

    [Fact]
    public async Task SnippetFallback_WriteConfigAsync_Throws()
    {
        var writer = new SnippetFallbackWriter();
        await Assert.ThrowsAsync<NotSupportedException>(
            () => writer.WriteConfigAsync("/tmp/test.json", DefaultEntry));
    }

    #endregion
}
