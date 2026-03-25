using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Infrastructure;
using QdrantSkillsMCP.Infrastructure.Embedding;
using Xunit;

namespace QdrantSkillsMCP.IntegrationTests;

/// <summary>
/// DI wiring integration tests that verify ServiceRegistration resolves the correct
/// IEmbeddingService implementation based on EmbeddingProvider configuration.
/// These tests validate wiring only -- no actual API keys or running services needed.
/// </summary>
public sealed class EmbeddingProviderIntegrationTests
{
    [Fact]
    public void OpenAI_Provider_Resolves_OpenAiEmbeddingService()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["QdrantSkills:EmbeddingProvider"] = "OpenAI",
            ["QdrantSkills:OpenAiApiKey"] = "sk-test-key-for-di-wiring-test"
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQdrantSkillsInfrastructure(config);

        using var sp = services.BuildServiceProvider();
        var embeddingService = sp.GetRequiredService<IEmbeddingService>();

        Assert.IsType<OpenAiEmbeddingService>(embeddingService);
    }

    [Fact]
    public void AzureOpenAI_WithMissingEndpoint_ThrowsOnResolve()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["QdrantSkills:EmbeddingProvider"] = "AzureOpenAI"
            // Missing AzureOpenAiEndpoint, AzureOpenAiApiKey, AzureOpenAiDeployment
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQdrantSkillsInfrastructure(config);

        using var sp = services.BuildServiceProvider();

        // Resolution should throw because endpoint is missing
        Assert.ThrowsAny<InvalidOperationException>(() =>
            sp.GetRequiredService<IEmbeddingService>());
    }

    [Fact]
    public void Ollama_Provider_Resolves_OllamaEmbeddingService()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["QdrantSkills:EmbeddingProvider"] = "Ollama"
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQdrantSkillsInfrastructure(config);

        using var sp = services.BuildServiceProvider();
        var embeddingService = sp.GetRequiredService<IEmbeddingService>();

        Assert.IsType<OllamaEmbeddingService>(embeddingService);
    }

    [Fact]
    public void NoProvider_Defaults_To_OnnxEmbeddingService()
    {
        // No EmbeddingProvider configured -- should default to LocalONNX
        // Note: This test will fail if the ONNX model path is unavailable,
        // because BertOnnxTextEmbeddingGenerationService.Create() tries to open the model file.
        // We only verify the IEmbeddingService registration type, not the generator.
        var config = BuildConfig(new Dictionary<string, string?>());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQdrantSkillsInfrastructure(config);

        // Check the service descriptor is registered for OnnxEmbeddingService
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IEmbeddingService) &&
            d.ImplementationType == typeof(OnnxEmbeddingService));

        Assert.NotNull(descriptor);
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }
}
