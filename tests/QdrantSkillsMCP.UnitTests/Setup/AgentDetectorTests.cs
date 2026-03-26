using NSubstitute;
using Xunit;
using QdrantSkillsMCP.Infrastructure.Setup;

namespace QdrantSkillsMCP.UnitTests.Setup;

public sealed class AgentDetectorTests
{
    [Fact]
    public void DetectInstalledAgents_WithMatchingWriters_ReturnsDetectedAgents()
    {
        // Arrange
        var writer = Substitute.For<IAgentConfigWriter>();
        writer.AgentName.Returns("TestAgent");
        writer.WriterId.Returns("test");
        writer.SupportedScopes.Returns([AgentScope.User]);
        writer.DetectInstallation(AgentScope.User).Returns("/home/user/.test/config.json");
        writer.SkillDirectoryPath.Returns((string?)null);

        var detector = new AgentDetector([writer]);

        // Act
        var agents = detector.DetectInstalledAgents();

        // Assert
        Assert.Single(agents);
        Assert.Equal("TestAgent", agents[0].Name);
        Assert.Equal("/home/user/.test/config.json", agents[0].ConfigPath);
        Assert.Equal(AgentScope.User, agents[0].Scope);
        Assert.Equal("test", agents[0].WriterId);
        Assert.Null(agents[0].SkillDirectoryPath);
    }

    [Fact]
    public void DetectInstalledAgents_WithNoMatches_ReturnsEmptyList()
    {
        // Arrange
        var writer = Substitute.For<IAgentConfigWriter>();
        writer.SupportedScopes.Returns([AgentScope.User, AgentScope.Project]);
        writer.DetectInstallation(Arg.Any<AgentScope>()).Returns((string?)null);

        var detector = new AgentDetector([writer]);

        // Act
        var agents = detector.DetectInstalledAgents();

        // Assert
        Assert.Empty(agents);
    }

    [Fact]
    public void DetectInstalledAgents_PropagatesSkillDirectoryPath()
    {
        // Arrange
        var writer = Substitute.For<IAgentConfigWriter>();
        writer.AgentName.Returns("Claude Code");
        writer.WriterId.Returns("claude");
        writer.SupportedScopes.Returns([AgentScope.User]);
        writer.DetectInstallation(AgentScope.User).Returns("/home/user/.claude.json");
        writer.SkillDirectoryPath.Returns("/home/user/.claude/skills/qdrant-skills-mcp");

        var detector = new AgentDetector([writer]);

        // Act
        var agents = detector.DetectInstalledAgents();

        // Assert
        Assert.Single(agents);
        Assert.Equal("/home/user/.claude/skills/qdrant-skills-mcp", agents[0].SkillDirectoryPath);
    }

    [Fact]
    public void DetectInstalledAgents_MultipleWriters_DetectsAll()
    {
        // Arrange
        var claude = Substitute.For<IAgentConfigWriter>();
        claude.AgentName.Returns("Claude Code");
        claude.WriterId.Returns("claude");
        claude.SupportedScopes.Returns([AgentScope.User, AgentScope.Project]);
        claude.DetectInstallation(AgentScope.User).Returns("/user/.claude.json");
        claude.DetectInstallation(AgentScope.Project).Returns((string?)null);
        claude.SkillDirectoryPath.Returns("/user/.claude/skills/qdrant-skills-mcp");

        var copilot = Substitute.For<IAgentConfigWriter>();
        copilot.AgentName.Returns("Copilot");
        copilot.WriterId.Returns("copilot");
        copilot.SupportedScopes.Returns([AgentScope.Project]);
        copilot.DetectInstallation(AgentScope.Project).Returns("/project/.vscode/mcp.json");
        copilot.SkillDirectoryPath.Returns((string?)null);

        var detector = new AgentDetector([claude, copilot]);

        // Act
        var agents = detector.DetectInstalledAgents();

        // Assert
        Assert.Equal(2, agents.Count);
        Assert.Contains(agents, a => a.WriterId == "claude" && a.Scope == AgentScope.User);
        Assert.Contains(agents, a => a.WriterId == "copilot" && a.Scope == AgentScope.Project);
    }

    [Fact]
    public void DetectInstalledAgents_NoWriters_ReturnsEmpty()
    {
        var detector = new AgentDetector([]);
        var agents = detector.DetectInstalledAgents();
        Assert.Empty(agents);
    }
}
