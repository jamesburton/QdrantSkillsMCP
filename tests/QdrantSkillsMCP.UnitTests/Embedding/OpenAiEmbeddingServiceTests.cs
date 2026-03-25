using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using QdrantSkillsMCP.Infrastructure.Configuration;
using QdrantSkillsMCP.Infrastructure.Embedding;

namespace QdrantSkillsMCP.UnitTests.Embedding;

public sealed class OpenAiEmbeddingServiceTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_ReturnsVectorFromGenerator()
    {
        var expectedVector = new float[] { 0.1f, 0.2f, 0.3f };
        var embedding = new Embedding<float>(expectedVector);
        var generatedEmbeddings = new GeneratedEmbeddings<Embedding<float>>([embedding]);

        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        generator.GenerateAsync(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<EmbeddingGenerationOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<GeneratedEmbeddings<Embedding<float>>>(generatedEmbeddings));

        var options = Options.Create(new QdrantSkillsOptions { VectorDimensions = 3 });
        var service = new OpenAiEmbeddingService(generator, options);

        var result = await service.GenerateEmbeddingAsync("test text", CancellationToken.None);

        Assert.Equal(expectedVector, result);
    }

    [Fact]
    public void Dimensions_ReturnsValueFromOptions()
    {
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        var options = Options.Create(new QdrantSkillsOptions { VectorDimensions = 1536 });
        var service = new OpenAiEmbeddingService(generator, options);

        Assert.Equal(1536, service.Dimensions);
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
        var service = new OpenAiEmbeddingService(generator, options);

        await service.GenerateEmbeddingAsync("test", token);

        await generator.Received(1).GenerateAsync(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<EmbeddingGenerationOptions?>(),
            token);
    }
}
