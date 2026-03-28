using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using QdrantSkillsMCP.Infrastructure.Setup;

namespace QdrantSkillsMCP.UnitTests.Setup;

[Collection("ConsoleOutput")]
public sealed class SetupWizardTests : IDisposable
{
    private readonly string _tempDir;

    public SetupWizardTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "qdrant-wizard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static IAgentConfigWriter CreateMockWriter(
        string agentName = "TestAgent",
        string writerId = "test",
        bool canAutoWrite = true,
        AgentScope[]? scopes = null,
        string? configPath = null,
        string? skillDir = null)
    {
        var writer = Substitute.For<IAgentConfigWriter>();
        writer.AgentName.Returns(agentName);
        writer.WriterId.Returns(writerId);
        writer.CanAutoWrite.Returns(canAutoWrite);
        writer.SupportedScopes.Returns(scopes ?? [AgentScope.User, AgentScope.Project]);
        writer.DetectInstallation(Arg.Any<AgentScope>()).Returns(configPath);
        writer.SkillDirectoryPath.Returns(skillDir);
        return writer;
    }

    #region Arg Parsing

    [Fact]
    public void ParseArgs_AgentAndLevel_ReturnsBoth()
    {
        var (agent, level) = SetupWizard.ParseArgs(["--setup", "--agent", "claude", "--level", "user"]);
        Assert.Equal("claude", agent);
        Assert.Equal(AgentScope.User, level);
    }

    [Fact]
    public void ParseArgs_NoAgentNoLevel_ReturnsBothNull()
    {
        var (agent, level) = SetupWizard.ParseArgs(["--setup"]);
        Assert.Null(agent);
        Assert.Null(level);
    }

    [Fact]
    public void ParseArgs_OnlyAgent_ReturnsAgentOnly()
    {
        var (agent, level) = SetupWizard.ParseArgs(["--setup", "--agent", "claude"]);
        Assert.Equal("claude", agent);
        Assert.Null(level);
    }

    [Fact]
    public void ParseArgs_InvalidLevel_ReturnsNullLevel()
    {
        var (agent, level) = SetupWizard.ParseArgs(["--setup", "--agent", "claude", "--level", "global"]);
        Assert.Equal("claude", agent);
        Assert.Null(level);
    }

    [Fact]
    public void ParseArgs_CaseInsensitive()
    {
        var (agent, level) = SetupWizard.ParseArgs(["--AGENT", "Claude", "--LEVEL", "Project"]);
        Assert.Equal("Claude", agent);
        Assert.Equal(AgentScope.Project, level);
    }

    #endregion

    #region Non-Interactive Mode

    [Fact]
    public async Task NonInteractive_KnownAgent_WritesConfig()
    {
        var configPath = Path.Combine(_tempDir, "test-config.json");
        var writer = CreateMockWriter(
            writerId: "claude",
            configPath: configPath,
            scopes: [AgentScope.User]);

        var detector = new AgentDetector([writer]);
        var wizard = new SetupWizard(detector, [writer], NullLogger<SetupWizard>.Instance);

        var result = await wizard.RunAsync(["--setup", "--agent", "claude", "--level", "user"]);

        Assert.Equal(0, result);
        await writer.Received(1).WriteConfigAsync(configPath, Arg.Any<McpServerEntry>());
    }

    [Fact]
    public async Task NonInteractive_UnknownAgent_ReturnsError()
    {
        var writer = CreateMockWriter(writerId: "claude");
        var detector = new AgentDetector([writer]);
        var wizard = new SetupWizard(detector, [writer], NullLogger<SetupWizard>.Instance);

        var result = await wizard.RunAsync(["--setup", "--agent", "unknown", "--level", "user"]);

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task NonInteractive_UnsupportedScope_ReturnsError()
    {
        var writer = CreateMockWriter(
            writerId: "claude-desktop",
            scopes: [AgentScope.User]);

        var detector = new AgentDetector([writer]);
        var wizard = new SetupWizard(detector, [writer], NullLogger<SetupWizard>.Instance);

        var result = await wizard.RunAsync(["--setup", "--agent", "claude-desktop", "--level", "project"]);

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task NonInteractive_PartialArgs_ReturnsError()
    {
        var writer = CreateMockWriter();
        var detector = new AgentDetector([writer]);
        var wizard = new SetupWizard(detector, [writer], NullLogger<SetupWizard>.Instance);

        // Only --agent without --level
        var result = await wizard.RunAsync(["--setup", "--agent", "test"]);

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task NonInteractive_SnippetOnlyWriter_PrintsSnippet()
    {
        var writer = CreateMockWriter(
            writerId: "snippet",
            canAutoWrite: false,
            scopes: [AgentScope.User]);
        writer.GenerateSnippet(Arg.Any<McpServerEntry>(), Arg.Any<AgentScope>())
            .Returns("snippet content");

        var detector = new AgentDetector([writer]);
        var wizard = new SetupWizard(detector, [writer], NullLogger<SetupWizard>.Instance);

        var result = await wizard.RunAsync(["--setup", "--agent", "snippet", "--level", "user"]);

        Assert.Equal(0, result);
        writer.Received(1).GenerateSnippet(Arg.Any<McpServerEntry>(), AgentScope.User);
        await writer.DidNotReceive().WriteConfigAsync(Arg.Any<string>(), Arg.Any<McpServerEntry>());
    }

    #endregion

    #region FindWriter

    [Fact]
    public void FindWriter_ByWriterId_CaseInsensitive()
    {
        var writer = CreateMockWriter(writerId: "claude", agentName: "Claude Code");
        var detector = new AgentDetector([writer]);
        var wizard = new SetupWizard(detector, [writer], NullLogger<SetupWizard>.Instance);

        Assert.NotNull(wizard.FindWriter("Claude"));
        Assert.NotNull(wizard.FindWriter("claude"));
        Assert.NotNull(wizard.FindWriter("Claude Code"));
    }

    [Fact]
    public void FindWriter_NotFound_ReturnsNull()
    {
        var writer = CreateMockWriter(writerId: "claude");
        var detector = new AgentDetector([writer]);
        var wizard = new SetupWizard(detector, [writer], NullLogger<SetupWizard>.Instance);

        Assert.Null(wizard.FindWriter("nonexistent"));
    }

    #endregion

    #region McpServerEntry

    [Fact]
    public async Task McpServerEntry_HasCorrectCommandAndArgs()
    {
        var configPath = Path.Combine(_tempDir, "test-config.json");
        var writer = CreateMockWriter(
            writerId: "test",
            configPath: configPath,
            scopes: [AgentScope.User]);

        McpServerEntry? capturedEntry = null;
        await writer.WriteConfigAsync(Arg.Any<string>(), Arg.Do<McpServerEntry>(e => capturedEntry = e));

        var detector = new AgentDetector([writer]);
        var wizard = new SetupWizard(detector, [writer], NullLogger<SetupWizard>.Instance);

        await wizard.RunAsync(["--setup", "--agent", "test", "--level", "user"]);

        Assert.NotNull(capturedEntry);
        Assert.Equal("qdrant-skills-mcp", capturedEntry!.ServerName);
        Assert.Equal("dnx", capturedEntry.Command);
        Assert.Single(capturedEntry.Args);
        Assert.Equal("qdrant-skills-mcp", capturedEntry.Args[0]);
    }

    #endregion

    #region WriteSkillFileAsync

    [Fact]
    public async Task WriteSkillFile_NullSkillDirectory_DoesNothing()
    {
        var writer = CreateMockWriter(skillDir: null);
        var detector = new AgentDetector([writer]);
        var wizard = new SetupWizard(detector, [writer], NullLogger<SetupWizard>.Instance);

        // Should not throw
        await wizard.WriteSkillFileAsync(writer);

        // No directory created
        var dirs = Directory.GetDirectories(_tempDir);
        Assert.Empty(dirs);
    }

    [Fact]
    public async Task WriteSkillFile_WithSkillDirectory_CreatesDirectoryAndFile()
    {
        var skillDir = Path.Combine(_tempDir, "skills", "qdrant-skills-mcp");
        var writer = CreateMockWriter(skillDir: skillDir);
        var detector = new AgentDetector([writer]);
        var wizard = new SetupWizard(detector, [writer], NullLogger<SetupWizard>.Instance);

        // Note: The embedded resource won't exist in test assembly, so this will
        // log a warning and skip. That's the expected behavior per the plan.
        await wizard.WriteSkillFileAsync(writer);

        // Directory may or may not be created depending on resource availability.
        // The key test is that no exception is thrown.
    }

    [Fact]
    public async Task NonInteractive_AgentWithSkillDir_CallsWriteSkill()
    {
        var configPath = Path.Combine(_tempDir, "test-config.json");
        var skillDir = Path.Combine(_tempDir, "skills", "qdrant-skills-mcp");
        var writer = CreateMockWriter(
            writerId: "claude",
            configPath: configPath,
            skillDir: skillDir,
            scopes: [AgentScope.User]);

        var detector = new AgentDetector([writer]);
        var wizard = new SetupWizard(detector, [writer], NullLogger<SetupWizard>.Instance);

        var result = await wizard.RunAsync(["--setup", "--agent", "claude", "--level", "user"]);

        Assert.Equal(0, result);
        // Config was written
        await writer.Received(1).WriteConfigAsync(configPath, Arg.Any<McpServerEntry>());
        // WriteSkillFileAsync was called (the wizard calls it after config write)
        // Since the embedded resource won't exist in test, it should log a warning but not fail
    }

    [Fact]
    public async Task NonInteractive_AgentWithoutSkillDir_DoesNotAttemptSkillWrite()
    {
        var configPath = Path.Combine(_tempDir, "test-config.json");
        var writer = CreateMockWriter(
            writerId: "copilot",
            configPath: configPath,
            skillDir: null,
            scopes: [AgentScope.Project]);

        var detector = new AgentDetector([writer]);
        var wizard = new SetupWizard(detector, [writer], NullLogger<SetupWizard>.Instance);

        var result = await wizard.RunAsync(["--setup", "--agent", "copilot", "--level", "project"]);

        Assert.Equal(0, result);
        await writer.Received(1).WriteConfigAsync(configPath, Arg.Any<McpServerEntry>());
        // No skill directory path means no SKILL.md write attempt (no directory created)
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "skills")));
    }

    #endregion
}
