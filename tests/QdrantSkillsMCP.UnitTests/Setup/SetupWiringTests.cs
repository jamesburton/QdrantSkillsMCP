using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Infrastructure;
using QdrantSkillsMCP.Infrastructure.Setup;

namespace QdrantSkillsMCP.UnitTests.Setup;

public sealed class SetupWiringTests
{
    private ServiceProvider BuildSetupProvider()
    {
        var services = new ServiceCollection();
        services.AddSetupServices();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddSetupServices_Resolves_SetupWizard()
    {
        using var provider = BuildSetupProvider();
        var wizard = provider.GetRequiredService<SetupWizard>();
        Assert.NotNull(wizard);
    }

    [Fact]
    public void AddSetupServices_Resolves_AgentDetector()
    {
        using var provider = BuildSetupProvider();
        var detector = provider.GetRequiredService<AgentDetector>();
        Assert.NotNull(detector);
    }

    [Fact]
    public void AddSetupServices_Resolves_All_ConfigWriters()
    {
        using var provider = BuildSetupProvider();
        var writers = provider.GetRequiredService<IEnumerable<IAgentConfigWriter>>();
        Assert.True(writers.Count() >= 9,
            $"Expected at least 9 config writers but got {writers.Count()}");
    }

    [Fact]
    public void AddSetupServices_Does_Not_Register_ISkillRepository()
    {
        using var provider = BuildSetupProvider();
        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<ISkillRepository>());
    }
}
