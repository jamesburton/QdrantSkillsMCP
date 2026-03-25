using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using QdrantSkillsMCP.Infrastructure.Configuration;
using QdrantSkillsMCP.Infrastructure.Embedding;

namespace QdrantSkillsMCP.UnitTests.Embedding;

public sealed class OllamaEmbeddingServiceTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_ReturnsVectorFromGenerator()
    {
        var expectedVector = new float[] { 0.5f, 0.6f, 0.7f, 0.8f };
        var embedding = new Embedding<float>(expectedVector);
        var generatedEmbeddings = new GeneratedEmbeddings<Embedding<float>>([embedding]);

        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        generator.GenerateAsync(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<EmbeddingGenerationOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<GeneratedEmbeddings<Embedding<float>>>(generatedEmbeddings));

        var options = Options.Create(new QdrantSkillsOptions { VectorDimensions = 384 });
        var service = new OllamaEmbeddingService(generator, options);

        var result = await service.GenerateEmbeddingAsync("test text", CancellationToken.None);

        Assert.Equal(expectedVector, result);
    }

    [Fact]
    public void Dimensions_ReturnsConfiguredValue()
    {
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        var options = Options.Create(new QdrantSkillsOptions { VectorDimensions = 768 });
        var service = new OllamaEmbeddingService(generator, options);

        Assert.Equal(768, service.Dimensions);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var embedding = new Embedding<float>(new float[] { 1.0f });
        var generatedEmbeddings = new GeneratedEmbeddings<Embedding<float>>([embedding]);

        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        generator.GenerateAsync(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<EmbeddingGenerationOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<GeneratedEmbeddings<Embedding<float>>>(generatedEmbeddings));

        var options = Options.Create(new QdrantSkillsOptions { VectorDimensions = 1 });
        var service = new OllamaEmbeddingService(generator, options);

        await service.GenerateEmbeddingAsync("test", token);

        await generator.Received(1).GenerateAsync(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<EmbeddingGenerationOptions?>(),
            token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateEmbeddingAsync_ThrowsOnNullOrWhitespace(string? text)
    {
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        var options = Options.Create(new QdrantSkillsOptions { VectorDimensions = 384 });
        var service = new OllamaEmbeddingService(generator, options);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.GenerateEmbeddingAsync(text!, CancellationToken.None));
    }
}
