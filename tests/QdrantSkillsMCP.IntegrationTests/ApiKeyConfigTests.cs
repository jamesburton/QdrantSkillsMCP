using Microsoft.Extensions.Configuration;
using QdrantSkillsMCP.Infrastructure.Configuration;
using Xunit;

namespace QdrantSkillsMCP.IntegrationTests;

/// <summary>
/// Tests for QDR-03: API key configuration wiring.
/// Verifies that QdrantSkillsOptions correctly reads API key from configuration.
/// </summary>
[Trait("Category", "Aspire")]
public sealed class ApiKeyConfigTests
{
    [Fact]
    public void ApiKeyIsNullByDefault()
    {
        var options = new QdrantSkillsOptions();
        Assert.Null(options.QdrantApiKey);
    }

    [Fact]
    public void ApiKeyBindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QdrantSkills:QdrantApiKey"] = "test-secret-api-key-123"
            })
            .Build();

        var options = new QdrantSkillsOptions();
        config.GetSection(QdrantSkillsOptions.SectionName).Bind(options);

        Assert.Equal("test-secret-api-key-123", options.QdrantApiKey);
    }

    [Fact]
    public void QdrantClientAcceptsApiKeyInOptions()
    {
        // Verify that creating a QdrantClient with an API key does not throw.
        // In production, the API key is passed to QdrantClient constructor via ServiceRegistration.
        // Here we verify the configuration wiring path works end-to-end.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QdrantSkills:QdrantApiKey"] = "test-key",
                ["QdrantSkills:QdrantHost"] = "localhost",
                ["QdrantSkills:QdrantGrpcPort"] = "6334"
            })
            .Build();

        var options = new QdrantSkillsOptions();
        config.GetSection(QdrantSkillsOptions.SectionName).Bind(options);

        // This should not throw -- QdrantClient accepts null or valid API key
        var client = new Qdrant.Client.QdrantClient(
            options.QdrantHost,
            options.QdrantGrpcPort,
            apiKey: options.QdrantApiKey);

        Assert.NotNull(client);
    }
}
