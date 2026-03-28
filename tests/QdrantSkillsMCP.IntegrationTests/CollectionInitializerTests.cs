using Microsoft.Extensions.Options;
using Qdrant.Client.Grpc;
using QdrantSkillsMCP.Infrastructure.Configuration;
using QdrantSkillsMCP.Infrastructure.Qdrant;
using QdrantSkillsMCP.IntegrationTests.Fixtures;
using Xunit;

namespace QdrantSkillsMCP.IntegrationTests;

/// <summary>
/// Tests that CollectionInitializer creates Qdrant collections with correct vector dimensions
/// and payload indexes. Runs against a real Qdrant container via Aspire.
/// </summary>
[Collection(QdrantCollection.Name)]
[Trait("Category", "Aspire")]
public sealed class CollectionInitializerTests : IAsyncLifetime
{
    private readonly QdrantFixture _fixture;
    private readonly string _testCollectionName;

    public CollectionInitializerTests(QdrantFixture fixture)
    {
        _fixture = fixture;
        // Use a unique collection per test class to avoid interference
        _testCollectionName = $"col-init-test-{Guid.NewGuid():N}";
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _fixture.QdrantClient.DeleteCollectionAsync(_testCollectionName);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task CollectionCreatedWithCorrectDimensions()
    {
        // Arrange
        var options = CreateOptions();
        using var initializer = new CollectionInitializer(
            _fixture.QdrantClient, Options.Create(options));

        // Act
        await initializer.EnsureCollectionAsync(CancellationToken.None);

        // Assert
        var info = await _fixture.QdrantClient.GetCollectionInfoAsync(_testCollectionName);
        Assert.NotNull(info);

        var vectorSize = info.Config.Params.VectorsConfig.Params.Size;
        Assert.Equal((ulong)options.VectorDimensions, vectorSize);

        var distance = info.Config.Params.VectorsConfig.Params.Distance;
        Assert.Equal(Distance.Cosine, distance);
    }

    [Fact]
    public async Task CollectionCreationIsIdempotent()
    {
        // Arrange
        var options = CreateOptions();
        using var initializer = new CollectionInitializer(
            _fixture.QdrantClient, Options.Create(options));

        // Act -- call twice, no error on second call
        await initializer.EnsureCollectionAsync(CancellationToken.None);
        await initializer.EnsureCollectionAsync(CancellationToken.None);

        // Assert -- collection still exists and is valid
        var info = await _fixture.QdrantClient.GetCollectionInfoAsync(_testCollectionName);
        Assert.NotNull(info);
    }

    [Fact]
    public async Task PayloadIndexesCreated()
    {
        // Arrange
        var options = CreateOptions();
        using var initializer = new CollectionInitializer(
            _fixture.QdrantClient, Options.Create(options));

        // Act
        await initializer.EnsureCollectionAsync(CancellationToken.None);

        // Assert -- verify payload indexes exist by checking collection info
        var info = await _fixture.QdrantClient.GetCollectionInfoAsync(_testCollectionName);
        Assert.NotNull(info);

        // Qdrant reports payload schema with indexed fields
        var payloadSchema = info.PayloadSchema;
        Assert.True(payloadSchema.ContainsKey("name"), "Expected 'name' payload index");
        Assert.True(payloadSchema.ContainsKey("archived"), "Expected 'archived' payload index");
        Assert.True(payloadSchema.ContainsKey("updated_at"), "Expected 'updated_at' payload index");
    }

    private QdrantSkillsOptions CreateOptions() => new()
    {
        QdrantHost = _fixture.Options.QdrantHost,
        QdrantGrpcPort = _fixture.Options.QdrantGrpcPort,
        CollectionName = _testCollectionName,
        VectorDimensions = 64
    };
}
