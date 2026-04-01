using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QdrantSkillsMCP.Core.Models;
using QdrantSkillsMCP.Infrastructure.Configuration;
using QdrantSkillsMCP.Infrastructure.Qdrant;
using QdrantSkillsMCP.Infrastructure.Yaml;
using QdrantSkillsMCP.IntegrationTests.Fixtures;
using Xunit;

namespace QdrantSkillsMCP.IntegrationTests;

/// <summary>
/// End-to-end CRUD integration tests against a real Qdrant container.
/// Verifies add, retrieve, update, delete, and archive operations with lossless round-trip.
/// </summary>
[Collection(QdrantCollection.Name)]
[Trait("Category", "Aspire")]
public sealed class SkillCrudIntegrationTests : IAsyncLifetime
{
    private readonly QdrantFixture _fixture;
    private readonly QdrantSkillRepository _repository;
    private readonly FakeEmbeddingService _embeddingService;
    private readonly string _collectionName;

    public SkillCrudIntegrationTests(QdrantFixture fixture)
    {
        _fixture = fixture;
        _collectionName = $"crud-test-{Guid.NewGuid():N}";
        _embeddingService = new FakeEmbeddingService(fixture.Options.VectorDimensions);

        var options = new QdrantSkillsOptions
        {
            QdrantHost = fixture.Options.QdrantHost,
            QdrantGrpcPort = fixture.Options.QdrantGrpcPort,
            CollectionName = _collectionName,
            VectorDimensions = fixture.Options.VectorDimensions
        };

        var collectionInitializer = new CollectionInitializer(
            fixture.QdrantOperations, Options.Create(options));

        _repository = new QdrantSkillRepository(
            fixture.QdrantOperations,
            Options.Create(options),
            collectionInitializer,
            new SkillParser(),
            NullLogger<QdrantSkillRepository>.Instance);
    }

    public async ValueTask InitializeAsync()
    {
        await _repository.EnsureCollectionAsync(CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _fixture.QdrantClient.DeleteCollectionAsync(_collectionName);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task AddAndRetrieveSkill_RoundTripsLosslessly()
    {
        // Arrange
        var rawContent = """
            ---
            name: test-roundtrip
            description: A test skill for round-trip verification
            tags:
              - testing
              - integration
            ---
            # Test Skill

            This is the **markdown body** with special chars: <>&"'

            ```csharp
            var x = 42;
            ```
            """;

        var skill = CreateSkill("test-roundtrip", rawContent);
        var embedding = await _embeddingService.GenerateEmbeddingAsync(rawContent, CancellationToken.None);

        // Act
        await _repository.AddAsync(skill, embedding, overwrite: false, CancellationToken.None);
        var retrieved = await _repository.GetByNameAsync("test-roundtrip", CancellationToken.None);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(rawContent, retrieved.RawContent);
        Assert.Equal("test-roundtrip", retrieved.Name);
        Assert.Equal("A test skill for round-trip verification", retrieved.Description);
    }

    [Fact]
    public async Task AddSkill_DuplicateName_ThrowsWithoutOverwrite()
    {
        // Arrange
        var skill = CreateSkill("duplicate-test", "# Duplicate Test\nFirst version");
        var embedding = await _embeddingService.GenerateEmbeddingAsync("duplicate", CancellationToken.None);
        await _repository.AddAsync(skill, embedding, overwrite: false, CancellationToken.None);

        // Act & Assert
        var duplicate = CreateSkill("duplicate-test", "# Duplicate Test\nSecond version");
        var dupEmbedding = await _embeddingService.GenerateEmbeddingAsync("duplicate2", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repository.AddAsync(duplicate, dupEmbedding, overwrite: false, CancellationToken.None));
    }

    [Fact]
    public async Task AddSkill_DuplicateName_SucceedsWithOverwrite()
    {
        // Arrange
        var skill = CreateSkill("overwrite-test", "# Original\nOriginal content");
        var embedding = await _embeddingService.GenerateEmbeddingAsync("original", CancellationToken.None);
        await _repository.AddAsync(skill, embedding, overwrite: false, CancellationToken.None);

        // Act
        var updated = CreateSkill("overwrite-test", "# Updated\nUpdated content via overwrite");
        var updEmbedding = await _embeddingService.GenerateEmbeddingAsync("updated", CancellationToken.None);
        await _repository.AddAsync(updated, updEmbedding, overwrite: true, CancellationToken.None);

        // Assert
        var retrieved = await _repository.GetByNameAsync("overwrite-test", CancellationToken.None);
        Assert.NotNull(retrieved);
        Assert.Equal("# Updated\nUpdated content via overwrite", retrieved.RawContent);
    }

    [Fact]
    public async Task UpdateSkill_ChangesContent()
    {
        // Arrange
        var skill = CreateSkill("update-test", "# Original\nOriginal content");
        var embedding = await _embeddingService.GenerateEmbeddingAsync("original", CancellationToken.None);
        await _repository.AddAsync(skill, embedding, overwrite: false, CancellationToken.None);

        // Act
        var updated = CreateSkill("update-test", "# Updated\nNew and improved content");
        var updEmbedding = await _embeddingService.GenerateEmbeddingAsync("new and improved", CancellationToken.None);
        await _repository.UpdateAsync(updated, updEmbedding, CancellationToken.None);

        // Assert
        var retrieved = await _repository.GetByNameAsync("update-test", CancellationToken.None);
        Assert.NotNull(retrieved);
        Assert.Equal("# Updated\nNew and improved content", retrieved.RawContent);
    }

    [Fact]
    public async Task DeleteSkill_RemovesPermanently()
    {
        // Arrange
        var skill = CreateSkill("delete-test", "# Delete Me\nTemporary content");
        var embedding = await _embeddingService.GenerateEmbeddingAsync("delete", CancellationToken.None);
        await _repository.AddAsync(skill, embedding, overwrite: false, CancellationToken.None);

        // Act
        await _repository.DeleteAsync("delete-test", CancellationToken.None);

        // Assert
        var retrieved = await _repository.GetByNameAsync("delete-test", CancellationToken.None);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task ArchiveSkill_ExcludesFromSearch()
    {
        // Arrange
        var skill = CreateSkill("archive-search-test", "# Archive Search\nSearchable content about widgets");
        var embedding = await _embeddingService.GenerateEmbeddingAsync("searchable content about widgets", CancellationToken.None);
        await _repository.AddAsync(skill, embedding, overwrite: false, CancellationToken.None);

        // Act
        await _repository.ArchiveAsync("archive-search-test", CancellationToken.None);

        // Search for it
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync("searchable content about widgets", CancellationToken.None);
        var results = await _repository.SearchAsync(queryEmbedding, 10, scoreThreshold: null, CancellationToken.None);

        // Assert
        Assert.DoesNotContain(results, r => r.Skill.Name == "archive-search-test");
    }

    [Fact]
    public async Task ArchiveSkill_ExcludesFromList()
    {
        // Arrange
        var skill = CreateSkill("archive-list-test", "# Archive List\nListable content");
        var embedding = await _embeddingService.GenerateEmbeddingAsync("listable", CancellationToken.None);
        await _repository.AddAsync(skill, embedding, overwrite: false, CancellationToken.None);

        // Act
        await _repository.ArchiveAsync("archive-list-test", CancellationToken.None);
        var list = await _repository.ListAsync(CancellationToken.None);

        // Assert
        Assert.DoesNotContain(list, m => m.Name == "archive-list-test");
    }

    private static Skill CreateSkill(string name, string rawContent) => new()
    {
        Name = name,
        Description = null,
        Tags = null,
        RawContent = rawContent,
        MarkdownBody = rawContent, // Simplified for tests
        UpdatedAt = DateTimeOffset.UtcNow,
        Archived = false
    };
}
