using Microsoft.Extensions.Configuration;
using QdrantSkillsMCP.Infrastructure.Configuration;
using QdrantSkillsMCP.IntegrationTests.Fixtures;
using Xunit;

namespace QdrantSkillsMCP.IntegrationTests;

/// <summary>
/// Tests for QDR-01: configurable Qdrant host and port.
/// Verifies the QdrantClient connects with configured options and defaults are correct.
/// </summary>
[Collection(QdrantCollection.Name)]
public sealed class QdrantConnectionTests
{
    private readonly QdrantFixture _fixture;

    public QdrantConnectionTests(QdrantFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task QdrantClientConnectsWithConfiguredHostAndPort()
    {
        // The fixture QdrantClient was created from Aspire-configured host/port.
        // If it can list collections, the connection works end-to-end.
        var collections = await _fixture.QdrantClient.ListCollectionsAsync();
        Assert.NotNull(collections);
    }

    [Fact]
    public void QdrantSkillsOptionsDefaultValues()
    {
        var options = new QdrantSkillsOptions();

        Assert.Equal("localhost", options.QdrantHost);
        Assert.Equal(6334, options.QdrantGrpcPort);
    }

    [Fact]
    public void QdrantSkillsOptionsBindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QdrantSkills:QdrantHost"] = "custom-host.example.com",
                ["QdrantSkills:QdrantGrpcPort"] = "9334"
            })
            .Build();

        var options = new QdrantSkillsOptions();
        config.GetSection(QdrantSkillsOptions.SectionName).Bind(options);

        Assert.Equal("custom-host.example.com", options.QdrantHost);
        Assert.Equal(9334, options.QdrantGrpcPort);
    }
}
