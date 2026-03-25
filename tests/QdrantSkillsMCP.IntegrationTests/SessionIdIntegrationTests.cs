using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QdrantSkillsMCP.Core.Models;
using QdrantSkillsMCP.Infrastructure.Configuration;
using QdrantSkillsMCP.Infrastructure.Qdrant;
using QdrantSkillsMCP.Infrastructure.Session;
using QdrantSkillsMCP.Infrastructure.Tools;
using QdrantSkillsMCP.Infrastructure.Yaml;
using QdrantSkillsMCP.IntegrationTests.Fixtures;
using Xunit;

namespace QdrantSkillsMCP.IntegrationTests;

/// <summary>
/// Integration tests verifying keyed session tracking end-to-end with real Qdrant.
/// Tests search/load/reset with sessionId, output modes, and ALREADY LOADED behavior.
/// </summary>
[Collection(QdrantCollection.Name)]
public sealed class SessionIdIntegrationTests : IAsyncLifetime
{
    private readonly QdrantFixture _fixture;
    private readonly QdrantSkillRepository _repository;
    private readonly FakeEmbeddingService _embeddingService;
    private readonly InMemorySessionTracker _sessionTracker;
    private readonly SkillSearchTools _searchTools;
    private readonly SessionTools _sessionTools;
    private readonly string _collectionName;

    public SessionIdIntegrationTests(QdrantFixture fixture)
    {
        _fixture = fixture;
        _collectionName = $"session-test-{Guid.NewGuid():N}";
        _embeddingService = new FakeEmbeddingService(fixture.Options.VectorDimensions);
        _sessionTracker = new InMemorySessionTracker();

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

        _searchTools = new SkillSearchTools(
            _repository,
            _embeddingService,
            _sessionTracker,
            NullLogger<SkillSearchTools>.Instance);

        _sessionTools = new SessionTools(
            _sessionTracker,
            NullLogger<SessionTools>.Instance);
    }

    public async ValueTask InitializeAsync()
    {
        await _repository.EnsureCollectionAsync(CancellationToken.None);

        // Seed test skills
        await AddSkillAsync("python-basics", "Python programming basics loops variables functions");
        await AddSkillAsync("javascript-async", "JavaScript async await promises callbacks event loop");
        await AddSkillAsync("docker-compose", "Docker compose multi-container orchestration yaml");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _fixture.QdrantClient.DeleteCollectionAsync(_collectionName);
        }
        catch { /* Ignore cleanup errors */ }
    }

    [Fact]
    public async Task SearchFull_MarksSkillsAsLoaded_SecondSearchShowsAlreadyLoaded()
    {
        // First search in full mode marks skills as loaded in default session
        var result1 = await _searchTools.SearchSkills("Python programming", maxResults: 1, outputMode: "full");

        Assert.DoesNotContain("ALREADY LOADED", result1);

        // Second search should show the previously loaded skill in ALREADY LOADED prefix
        var result2 = await _searchTools.SearchSkills("Docker containers", maxResults: 1, outputMode: "full");

        Assert.Contains("ALREADY LOADED SKILLS:", result2);
        Assert.Contains("python-basics", result2);
    }

    [Fact]
    public async Task SearchWithSessionIds_IsolatesLoadedSkillsBetweenSessions()
    {
        // Search in session A
        await _searchTools.SearchSkills("Python programming", maxResults: 1, outputMode: "full", sessionId: "session-a");

        // Search in session B -- should NOT see session A's loaded skills
        var resultB = await _searchTools.SearchSkills("Docker containers", maxResults: 1, outputMode: "full", sessionId: "session-b");

        Assert.DoesNotContain("ALREADY LOADED", resultB);
    }

    [Fact]
    public async Task SearchOutputModeNames_ReturnsStringArray_DoesNotMarkLoaded()
    {
        var result = await _searchTools.SearchSkills("Python programming", maxResults: 2, outputMode: "names");

        // Should be a JSON string array
        var names = JsonSerializer.Deserialize<string[]>(result);
        Assert.NotNull(names);
        Assert.NotEmpty(names);

        // Skills should NOT be marked as loaded (names mode is read-only)
        var loaded = _sessionTracker.GetLoadedSkills();
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task SearchOutputModeSummaries_ReturnsSummaryObjects_DoesNotMarkLoaded()
    {
        var result = await _searchTools.SearchSkills("JavaScript async", maxResults: 2, outputMode: "summaries");

        // Parse as array of objects with name field
        using var doc = JsonDocument.Parse(result);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.GetArrayLength() > 0);

        var firstItem = doc.RootElement[0];
        Assert.True(firstItem.TryGetProperty("name", out _));

        // Skills should NOT be marked as loaded (summaries mode is read-only)
        var loaded = _sessionTracker.GetLoadedSkills();
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task LoadSkill_WithCustomSessionId_MarksInCorrectSession()
    {
        await _searchTools.LoadSkill("python-basics", sessionId: "custom");

        // Check it's loaded in the custom session
        Assert.True(_sessionTracker.IsLoaded("python-basics", "custom"));

        // Check it's NOT loaded in the default session
        var defaultLoaded = _sessionTracker.GetLoadedSkills();
        Assert.Empty(defaultLoaded);
    }

    [Fact]
    public async Task ResetSession_ClearsLoadedSkills_NextSearchShowsEmptyAlreadyLoaded()
    {
        // Load some skills in default session
        await _searchTools.SearchSkills("Python programming", maxResults: 1, outputMode: "full");
        Assert.NotEmpty(_sessionTracker.GetLoadedSkills());

        // Reset default session
        _sessionTools.ResetSession();

        // After reset, loaded skills should be empty
        Assert.Empty(_sessionTracker.GetLoadedSkills());

        // Next search should not show ALREADY LOADED
        var result = await _searchTools.SearchSkills("Docker containers", maxResults: 1, outputMode: "full");
        Assert.DoesNotContain("ALREADY LOADED", result);
    }

    private async Task AddSkillAsync(string name, string rawContent)
    {
        var skill = new Skill
        {
            Name = name,
            Description = rawContent,
            Tags = null,
            RawContent = $"---\nname: {name}\n---\n{rawContent}",
            MarkdownBody = rawContent,
            UpdatedAt = DateTimeOffset.UtcNow,
            Archived = false
        };

        var embedding = await _embeddingService.GenerateEmbeddingAsync(rawContent, CancellationToken.None);
        await _repository.AddAsync(skill, embedding, overwrite: false, CancellationToken.None);
    }
}
