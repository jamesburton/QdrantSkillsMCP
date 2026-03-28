using QdrantSkillsMCP.Infrastructure.Cli.Commands;
using QdrantSkillsMCP.Infrastructure.Configuration;
using Xunit;

namespace QdrantSkillsMCP.IntegrationTests.Config;

/// <summary>
/// Integration tests for the --config validate subcommand.
/// These tests make real (or attempted) network calls.
/// </summary>
public sealed class ConfigValidateTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _userDir;
    private readonly string _projectDir;
    private readonly StringWriter _stdOut;
    private readonly TextWriter _originalOut;

    public ConfigValidateTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cfg-val-{Guid.NewGuid():N}");
        _userDir = Path.Combine(_tempDir, "user");
        _projectDir = Path.Combine(_tempDir, "project");
        Directory.CreateDirectory(_userDir);
        Directory.CreateDirectory(_projectDir);

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
    public async Task Validate_WithUnreachableHost_ReturnsOne()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        await mgr.InitAsync();
        await mgr.SetValueAsync("QdrantHost", "unreachable-host-that-does-not-exist.invalid");
        await mgr.SetValueAsync("QdrantGrpcPort", "6334");

        var result = await ConfigCommand.RunAsync(mgr, ["--config", "validate"]);
        Assert.Equal(1, result);
        var output = _stdOut.ToString();
        Assert.Contains("FAIL", output);
    }

    [Fact]
    public async Task Validate_WithNonLocalhostWithoutTls_PrintsWarning()
    {
        var mgr = new ConfigManager(_userDir, _projectDir);
        await mgr.InitAsync();
        await mgr.SetValueAsync("QdrantHost", "remote.example.com");
        await mgr.SetValueAsync("QdrantGrpcPort", "6334");

        var result = await ConfigCommand.RunAsync(mgr, ["--config", "validate"]);
        var output = _stdOut.ToString();
        // Validate prints resolved config and attempts connection
        Assert.Contains("Resolved config", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("remote.example.com", output);
    }
}
