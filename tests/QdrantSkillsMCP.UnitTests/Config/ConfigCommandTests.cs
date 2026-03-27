using QdrantSkillsMCP.Infrastructure.Cli.Commands;
using QdrantSkillsMCP.Infrastructure.Configuration;
using Xunit;

namespace QdrantSkillsMCP.UnitTests.Config;

/// <summary>
/// Tests for ConfigCommand subcommand dispatcher.
/// Uses [Collection] to prevent Console.Out race conditions in parallel test runs.
/// </summary>
[Collection("ConfigCommand")]
public sealed class ConfigCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _userDir;
    private readonly string _projectDir;
    private readonly ConfigManager _configManager;
    private readonly StringWriter _stdOut;
    private readonly TextWriter _originalOut;

    public ConfigCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cfg-cmd-{Guid.NewGuid():N}");
        _userDir = Path.Combine(_tempDir, "user");
        _projectDir = Path.Combine(_tempDir, "project");
        Directory.CreateDirectory(_userDir);
        Directory.CreateDirectory(_projectDir);
        _configManager = new ConfigManager(_userDir, _projectDir);

        _stdOut = new StringWriter();
        _originalOut = Console.Out;
        Console.SetOut(_stdOut);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        _stdOut.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Show_ReturnsZero_AndDisplaysConfigValues()
    {
        var result = await ConfigCommand.RunAsync(_configManager, ["--config", "show"]);
        Assert.Equal(0, result);
        var output = _stdOut.ToString();
        Assert.Contains("QdrantHost", output);
        Assert.Contains("[default]", output);
    }

    [Fact]
    public async Task Show_MasksSecrets_ByDefault()
    {
        await _configManager.SetValueAsync("OpenAiApiKey", "sk-abcdefghijklmnopqrstuvwxyz1234567890");
        var result = await ConfigCommand.RunAsync(_configManager, ["--config", "show"]);
        Assert.Equal(0, result);
        var output = _stdOut.ToString();
        // Should NOT contain full key
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuvwxyz1234567890", output);
        // Should contain masked format
        Assert.Contains("****", output);
    }

    [Fact]
    public async Task Show_Reveal_ShowsUnmaskedSecrets()
    {
        await _configManager.SetValueAsync("OpenAiApiKey", "sk-abcdefghijklmnopqrstuvwxyz1234567890");
        var result = await ConfigCommand.RunAsync(_configManager, ["--config", "show", "--reveal"]);
        Assert.Equal(0, result);
        var output = _stdOut.ToString();
        Assert.Contains("sk-abcdefghijklmnopqrstuvwxyz1234567890", output);
    }

    [Fact]
    public async Task Show_IncludesSourceAnnotations()
    {
        var result = await ConfigCommand.RunAsync(_configManager, ["--config", "show"]);
        Assert.Equal(0, result);
        var output = _stdOut.ToString();
        // Default values should show [default] source
        Assert.Contains("[default]", output);
    }

    [Fact]
    public async Task Set_WritesValue_ReturnsZero()
    {
        var result = await ConfigCommand.RunAsync(_configManager, ["--config", "set", "QdrantHost=cloud.example.com"]);
        Assert.Equal(0, result);
        Assert.Equal("cloud.example.com", _configManager.GetValue("QdrantHost"));
    }

    [Fact]
    public async Task Set_WithProjectFlag_WritesToProjectScope()
    {
        var result = await ConfigCommand.RunAsync(_configManager, ["--config", "set", "QdrantHost=cloud.example.com", "--project"]);
        Assert.Equal(0, result);
        // Project config file should exist
        var projectConfigPath = Path.Combine(_projectDir, "qdrant-skills.json");
        Assert.True(File.Exists(projectConfigPath));
    }

    [Fact]
    public async Task Get_ReturnsResolvedValue()
    {
        var result = await ConfigCommand.RunAsync(_configManager, ["--config", "get", "QdrantHost"]);
        Assert.Equal(0, result);
        var output = _stdOut.ToString();
        Assert.Contains("localhost", output);
    }

    [Fact]
    public async Task Init_CallsInitAsync_ReturnsZero()
    {
        var result = await ConfigCommand.RunAsync(_configManager, ["--config", "init"]);
        Assert.Equal(0, result);
        var configPath = Path.Combine(_userDir, "config.json");
        Assert.True(File.Exists(configPath));
    }

    [Fact]
    public async Task Reset_SpecificKey_RemovesKey()
    {
        await _configManager.SetValueAsync("QdrantHost", "cloud.example.com");
        var result = await ConfigCommand.RunAsync(_configManager, ["--config", "reset", "QdrantHost"]);
        Assert.Equal(0, result);
        // Should fall back to default
        Assert.Equal("localhost", _configManager.GetValue("QdrantHost"));
    }

    [Fact]
    public async Task Reset_NoKey_ResetsAll()
    {
        await _configManager.SetValueAsync("QdrantHost", "cloud.example.com");
        var result = await ConfigCommand.RunAsync(_configManager, ["--config", "reset"]);
        Assert.Equal(0, result);
        Assert.Equal("localhost", _configManager.GetValue("QdrantHost"));
    }

    [Fact]
    public async Task Use_SwitchesProfile_ReturnsZero()
    {
        var result = await ConfigCommand.RunAsync(_configManager, ["--config", "use", "cloud"]);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Env_GeneratesTemplate_ReturnsZero()
    {
        var result = await ConfigCommand.RunAsync(_configManager, ["--config", "env"]);
        Assert.Equal(0, result);
        var output = _stdOut.ToString();
        Assert.Contains("QDRANT_SKILLS__", output);
    }

    [Fact]
    public async Task UnknownSubcommand_PrintsUsage_ReturnsOne()
    {
        var result = await ConfigCommand.RunAsync(_configManager, ["--config", "foobar"]);
        Assert.Equal(1, result);
    }
}
