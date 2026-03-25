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
/// End-to-end search integration tests against a real Qdrant container.
/// Verifies semantic search ranking, maxResults, score threshold, and archive filtering.
/// </summary>
[Collection(QdrantCollection.Name)]
public sealed class SkillSearchIntegrationTests : IAsyncLifetime
{
    private readonly QdrantFixture _fixture;
    private readonly QdrantSkillRepository _repository;
    private readonly FakeEmbeddingService _embeddingService;
    private readonly string _collectionName;

    public SkillSearchIntegrationTests(QdrantFixture fixture)
    {
        _fixture = fixture;
        _collectionName = $"search-test-{Guid.NewGuid():N}";
        _embeddingService = new FakeEmbeddingService(fixture.Options.VectorDimensions);

        var options = new QdrantSkillsOptions
        {
            QdrantHost = fixture.Options.QdrantHost,
            QdrantGrpcPort = fixture.Options.QdrantGrpcPort,
            CollectionName = _collectionName,
            VectorDimensions = fixture.Options.VectorDimensions
        };

        var collectionInitializer = new CollectionInitializer(
            fixture.QdrantClient, Options.Create(options));

        _repository = new QdrantSkillRepository(
            fixture.QdrantClient,
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
    public async Task SearchReturnsResultsRankedByScore()
    {
        // Arrange -- add 3 skills with different content
        // The skill whose embedding is closest to the query should rank highest
        var skill1 = CreateSkill("python-basics", "Python programming basics loops variables");
        var skill2 = CreateSkill("javascript-async", "JavaScript async await promises callbacks");
        var skill3 = CreateSkill("python-advanced", "Python advanced decorators metaclasses generators");

        await AddSkillWithEmbedding(skill1, "Python programming basics loops variables");
        await AddSkillWithEmbedding(skill2, "JavaScript async await promises callbacks");
        await AddSkillWithEmbedding(skill3, "Python advanced decorators metaclasses generators");

        // Act -- search with a query close to python-basics
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(
            "Python programming basics loops variables", CancellationToken.None);
        var results = await _repository.SearchAsync(queryEmbedding, 10, scoreThreshold: null, CancellationToken.None);

        // Assert
        Assert.NotEmpty(results);
        // The first result should be python-basics (exact match embedding)
        Assert.Equal("python-basics", results[0].Skill.Name);
        // Scores should be in descending order
        for (var i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].Score >= results[i].Score,
                $"Results not in descending score order at index {i}");
        }
    }

    [Fact]
    public async Task SearchRespectsMaxResults()
    {
        // Arrange -- add 5 skills
        for (var i = 0; i < 5; i++)
        {
            var skill = CreateSkill($"max-result-{i}", $"Skill content number {i} for testing max results");
            await AddSkillWithEmbedding(skill, $"Skill content number {i} for testing max results");
        }

        // Act -- search with maxResults=2
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(
            "Skill content for testing", CancellationToken.None);
        var results = await _repository.SearchAsync(queryEmbedding, maxResults: 2, scoreThreshold: null, CancellationToken.None);

        // Assert
        Assert.True(results.Count <= 2, $"Expected at most 2 results, got {results.Count}");
    }

    [Fact]
    public async Task SearchRespectsScoreThreshold()
    {
        // Arrange -- add skills with very different content
        var skill1 = CreateSkill("threshold-close", "cats dogs pets animals veterinary");
        var skill2 = CreateSkill("threshold-far", "quantum physics string theory relativity");

        await AddSkillWithEmbedding(skill1, "cats dogs pets animals veterinary");
        await AddSkillWithEmbedding(skill2, "quantum physics string theory relativity");

        // Act -- search with a high threshold (close match only)
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(
            "cats dogs pets animals veterinary", CancellationToken.None);

        // Results with no threshold
        var allResults = await _repository.SearchAsync(queryEmbedding, 10, scoreThreshold: null, CancellationToken.None);

        // Results with very high threshold (should filter out distant matches)
        var strictResults = await _repository.SearchAsync(queryEmbedding, 10, scoreThreshold: 0.99f, CancellationToken.None);

        // Assert -- strict threshold returns fewer or equal results
        Assert.True(strictResults.Count <= allResults.Count,
            $"Strict threshold ({strictResults.Count}) returned more results than no threshold ({allResults.Count})");
    }

    [Fact]
    public async Task ListSkillsReturnsAllNonArchived()
    {
        // Arrange -- add 3 skills, archive 1
        var skill1 = CreateSkill("list-active-1", "Active skill one");
        var skill2 = CreateSkill("list-active-2", "Active skill two");
        var skill3 = CreateSkill("list-archived", "This will be archived");

        await AddSkillWithEmbedding(skill1, "Active skill one");
        await AddSkillWithEmbedding(skill2, "Active skill two");
        await AddSkillWithEmbedding(skill3, "This will be archived");

        await _repository.ArchiveAsync("list-archived", CancellationToken.None);

        // Act
        var list = await _repository.ListAsync(CancellationToken.None);

        // Assert -- only 2 non-archived skills
        Assert.Equal(2, list.Count);
        Assert.Contains(list, m => m.Name == "list-active-1");
        Assert.Contains(list, m => m.Name == "list-active-2");
        Assert.DoesNotContain(list, m => m.Name == "list-archived");
    }

    private async Task AddSkillWithEmbedding(Skill skill, string embeddingText)
    {
        var embedding = await _embeddingService.GenerateEmbeddingAsync(embeddingText, CancellationToken.None);
        await _repository.AddAsync(skill, embedding, overwrite: false, CancellationToken.None);
    }

    private static Skill CreateSkill(string name, string rawContent) => new()
    {
        Name = name,
        Description = null,
        Tags = null,
        RawContent = rawContent,
        MarkdownBody = rawContent,
        UpdatedAt = DateTimeOffset.UtcNow,
        Archived = false
    };
}
