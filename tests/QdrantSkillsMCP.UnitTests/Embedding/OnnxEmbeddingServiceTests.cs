using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using QdrantSkillsMCP.Infrastructure.Configuration;
using QdrantSkillsMCP.Infrastructure.Embedding;

namespace QdrantSkillsMCP.UnitTests.Embedding;

public sealed class OnnxEmbeddingServiceTests
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

        var options = Options.Create(new QdrantSkillsOptions());
        var service = new OnnxEmbeddingService(generator, options);

        var result = await service.GenerateEmbeddingAsync("test text", CancellationToken.None);

        Assert.Equal(expectedVector, result);
    }

    [Fact]
    public void Dimensions_Returns384_WhenDefaultConfig()
    {
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        var options = Options.Create(new QdrantSkillsOptions { VectorDimensions = 1536 });
        var service = new OnnxEmbeddingService(generator, options);

        Assert.Equal(384, service.Dimensions);
    }

    [Fact]
    public void Dimensions_Returns384_WhenAlreadySet()
    {
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        var options = Options.Create(new QdrantSkillsOptions { VectorDimensions = 384 });
        var service = new OnnxEmbeddingService(generator, options);

        Assert.Equal(384, service.Dimensions);
    }

    [Fact]
    public void Dimensions_ReturnsCustom_WhenExplicitlyOverridden()
    {
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        var options = Options.Create(new QdrantSkillsOptions { VectorDimensions = 768 });
        var service = new OnnxEmbeddingService(generator, options);

        Assert.Equal(768, service.Dimensions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateEmbeddingAsync_ThrowsOnNullOrWhitespace(string? text)
    {
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        var options = Options.Create(new QdrantSkillsOptions());
        var service = new OnnxEmbeddingService(generator, options);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.GenerateEmbeddingAsync(text!, CancellationToken.None));
    }
}
