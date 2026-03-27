using QdrantSkillsMCP.Infrastructure.Configuration;
using Xunit;

namespace QdrantSkillsMCP.UnitTests.Config;

/// <summary>
/// Tests for ShellDetector shell identification and env var template generation.
/// </summary>
public sealed class ShellDetectorTests
{
    [Fact]
    public void DetectShell_ReturnsPowerShell_WhenPSModulePathSet()
    {
        var shell = ShellDetector.DetectShell(
            key => key == "PSModulePath" ? "C:\\something" : null,
            isWindows: true);

        Assert.Equal(ShellType.PowerShell, shell);
    }

    [Fact]
    public void DetectShell_ReturnsBash_WhenShellEndsWith_Bash()
    {
        var shell = ShellDetector.DetectShell(
            key => key == "SHELL" ? "/bin/bash" : null,
            isWindows: false);

        Assert.Equal(ShellType.Bash, shell);
    }

    [Fact]
    public void DetectShell_ReturnsZsh_WhenShellEndsWith_Zsh()
    {
        var shell = ShellDetector.DetectShell(
            key => key == "SHELL" ? "/bin/zsh" : null,
            isWindows: false);

        Assert.Equal(ShellType.Zsh, shell);
    }

    [Fact]
    public void DetectShell_ReturnsCmd_OnWindows_WhenNoPowerShellOrShell()
    {
        var shell = ShellDetector.DetectShell(
            key => null,
            isWindows: true);

        Assert.Equal(ShellType.Cmd, shell);
    }

    [Fact]
    public void DetectShell_ReturnsBash_AsFallback()
    {
        var shell = ShellDetector.DetectShell(
            key => null,
            isWindows: false);

        Assert.Equal(ShellType.Bash, shell);
    }

    [Fact]
    public void GenerateEnvTemplate_Bash_ProducesExportLines()
    {
        var values = new Dictionary<string, string?>
        {
            ["QdrantHost"] = "localhost",
            ["QdrantApiKey"] = null
        };

        var result = ShellDetector.GenerateEnvTemplate(ShellType.Bash, values);

        Assert.Contains("export QDRANT_SKILLS__QdrantHost=\"localhost\"", result);
        Assert.Contains("# export QDRANT_SKILLS__QdrantApiKey=\"\"", result);
    }

    [Fact]
    public void GenerateEnvTemplate_PowerShell_ProducesEnvLines()
    {
        var values = new Dictionary<string, string?>
        {
            ["QdrantHost"] = "localhost",
            ["QdrantApiKey"] = null
        };

        var result = ShellDetector.GenerateEnvTemplate(ShellType.PowerShell, values);

        Assert.Contains("$env:QDRANT_SKILLS__QdrantHost = \"localhost\"", result);
        Assert.Contains("# $env:QDRANT_SKILLS__QdrantApiKey = \"\"", result);
    }

    [Fact]
    public void GenerateEnvTemplate_Cmd_ProducesSetLines()
    {
        var values = new Dictionary<string, string?>
        {
            ["QdrantHost"] = "localhost",
            ["QdrantApiKey"] = null
        };

        var result = ShellDetector.GenerateEnvTemplate(ShellType.Cmd, values);

        Assert.Contains("set QDRANT_SKILLS__QdrantHost=localhost", result);
        Assert.Contains("REM set QDRANT_SKILLS__QdrantApiKey=", result);
    }

    [Fact]
    public void GenerateEnvTemplate_Zsh_ProducesExportLines()
    {
        // Zsh uses same format as Bash
        var values = new Dictionary<string, string?> { ["QdrantHost"] = "myhost" };
        var result = ShellDetector.GenerateEnvTemplate(ShellType.Zsh, values);

        Assert.Contains("export QDRANT_SKILLS__QdrantHost=\"myhost\"", result);
    }

    [Fact]
    public void GenerateEnvTemplate_CoversAllConfigurableKeys()
    {
        var values = new Dictionary<string, string?>();
        var result = ShellDetector.GenerateEnvTemplate(ShellType.Bash, values);

        foreach (var key in ConfigManager.ConfigurableKeys)
        {
            Assert.Contains($"QDRANT_SKILLS__{key}", result);
        }
    }
}
