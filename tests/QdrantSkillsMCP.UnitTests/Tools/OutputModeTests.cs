using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Core.Models;
using QdrantSkillsMCP.Infrastructure.Session;
using QdrantSkillsMCP.Infrastructure.Tools;

namespace QdrantSkillsMCP.UnitTests.Tools;

/// <summary>
/// Tests for output mode behavior (full/names/summaries) in search and list tools,
/// sessionId parameter forwarding, and the reset-session tool.
/// </summary>
public sealed class OutputModeTests
{
    private readonly ISkillRepository _repository = Substitute.For<ISkillRepository>();
    private readonly IEmbeddingService _embeddingService = Substitute.For<IEmbeddingService>();
    private readonly InMemorySessionTracker _sessionTracker = new();

    private SkillSearchTools CreateSearchTools() =>
        new(_repository, _embeddingService, _sessionTracker, NullLogger<SkillSearchTools>.Instance);

    private SessionTools CreateSessionTools() =>
        new(_sessionTracker, NullLogger<SessionTools>.Instance);

    private static readonly Skill SampleSkill = new()
    {
        Name = "test-skill",
        Description = "A test skill",
        Tags = ["test", "sample"],
        RawContent = "---\nname: test-skill\n---\n# Test",
        MarkdownBody = "# Test",
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static readonly Skill SampleSkill2 = new()
    {
        Name = "another-skill",
        Description = "Another skill",
        Tags = ["other"],
        RawContent = "---\nname: another-skill\n---\n# Another",
        MarkdownBody = "# Another",
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private void SetupSearchReturns(params Skill[] skills)
    {
        var results = skills.Select((s, i) => new SearchResult { Skill = s, Score = 0.9f - i * 0.1f }).ToList();
        _embeddingService.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new float[] { 0.1f, 0.2f });
        _repository.SearchAsync(Arg.Any<float[]>(), Arg.Any<int>(), Arg.Any<float?>(), Arg.Any<CancellationToken>())
            .Returns(results);
    }

    private void SetupListReturns(params Skill[] skills)
    {
        var metadata = skills.Select(s => new SkillMetadata
        {
            Name = s.Name,
            Description = s.Description,
            Tags = s.Tags,
            UpdatedAt = s.UpdatedAt
        }).ToList();
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns(metadata);
    }

    // --- SearchSkills outputMode tests ---

    [Fact]
    public async Task SearchSkills_NamesMode_ReturnsStringArray()
    {
        SetupSearchReturns(SampleSkill, SampleSkill2);
        var tools = CreateSearchTools();

        var result = await tools.SearchSkills("test", outputMode: "names");

        var names = JsonSerializer.Deserialize<string[]>(result);
        Assert.NotNull(names);
        Assert.Equal(2, names.Length);
        Assert.Contains("test-skill", names);
        Assert.Contains("another-skill", names);
    }

    [Fact]
    public async Task SearchSkills_SummariesMode_ReturnsObjectsWithoutRawContent()
    {
        SetupSearchReturns(SampleSkill);
        var tools = CreateSearchTools();

        var result = await tools.SearchSkills("test", outputMode: "summaries");

        using var doc = JsonDocument.Parse(result);
        var arr = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(1, arr.GetArrayLength());

        var item = arr[0];
        Assert.Equal("test-skill", item.GetProperty("name").GetString());
        Assert.Equal("A test skill", item.GetProperty("description").GetString());
        Assert.True(item.TryGetProperty("score", out _));
        Assert.False(item.TryGetProperty("rawContent", out _));
    }

    [Fact]
    public async Task SearchSkills_FullMode_ReturnsContentAndMarksLoaded()
    {
        SetupSearchReturns(SampleSkill);
        var tools = CreateSearchTools();

        var result = await tools.SearchSkills("test", outputMode: "full");

        using var doc = JsonDocument.Parse(result);
        var results = doc.RootElement.GetProperty("results");
        Assert.Equal(1, results.GetArrayLength());
        Assert.True(results[0].TryGetProperty("rawContent", out var rc));
        Assert.False(string.IsNullOrEmpty(rc.GetString()));

        // Should be marked as loaded in default session
        Assert.True(_sessionTracker.IsLoaded("test-skill"));
    }

    [Fact]
    public async Task SearchSkills_NamesMode_DoesNotCallMarkLoaded()
    {
        SetupSearchReturns(SampleSkill);
        var tools = CreateSearchTools();

        await tools.SearchSkills("test", outputMode: "names");

        Assert.False(_sessionTracker.IsLoaded("test-skill"));
    }

    [Fact]
    public async Task SearchSkills_SummariesMode_DoesNotCallMarkLoaded()
    {
        SetupSearchReturns(SampleSkill);
        var tools = CreateSearchTools();

        await tools.SearchSkills("test", outputMode: "summaries");

        Assert.False(_sessionTracker.IsLoaded("test-skill"));
    }

    [Fact]
    public async Task SearchSkills_InvalidOutputMode_DefaultsToFull()
    {
        SetupSearchReturns(SampleSkill);
        var tools = CreateSearchTools();

        var result = await tools.SearchSkills("test", outputMode: "bogus");

        // Full mode returns an object with "results" array containing rawContent
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("results", out _));
        Assert.True(_sessionTracker.IsLoaded("test-skill"));
    }

    [Fact]
    public async Task SearchSkills_CaseInsensitiveOutputMode()
    {
        SetupSearchReturns(SampleSkill);
        var tools = CreateSearchTools();

        var result = await tools.SearchSkills("test", outputMode: "NAMES");

        var names = JsonSerializer.Deserialize<string[]>(result);
        Assert.NotNull(names);
        Assert.Contains("test-skill", names);
        Assert.False(_sessionTracker.IsLoaded("test-skill"));
    }

    // --- ListSkills outputMode tests ---

    [Fact]
    public async Task ListSkills_NamesMode_ReturnsNameArray()
    {
        SetupListReturns(SampleSkill, SampleSkill2);
        var tools = CreateSearchTools();

        var result = await tools.ListSkills(outputMode: "names");

        var names = JsonSerializer.Deserialize<string[]>(result);
        Assert.NotNull(names);
        Assert.Equal(2, names.Length);
        Assert.Contains("test-skill", names);
        Assert.Contains("another-skill", names);
    }

    [Fact]
    public async Task ListSkills_SummariesMode_ReturnsNameAndDescription()
    {
        SetupListReturns(SampleSkill);
        var tools = CreateSearchTools();

        var result = await tools.ListSkills(outputMode: "summaries");

        using var doc = JsonDocument.Parse(result);
        var skills = doc.RootElement.GetProperty("skills");
        Assert.Equal(1, skills.GetArrayLength());

        var item = skills[0];
        Assert.Equal("test-skill", item.GetProperty("name").GetString());
        Assert.Equal("A test skill", item.GetProperty("description").GetString());
        // Summaries should not include tags or updatedAt
        Assert.False(item.TryGetProperty("tags", out _));
        Assert.Equal(default, item.GetProperty("updatedAt").GetDateTimeOffset());
    }

    // --- sessionId parameter tests ---

    [Fact]
    public async Task SearchSkills_FullMode_WithSessionId_MarksLoadedInCorrectSession()
    {
        SetupSearchReturns(SampleSkill);
        var tools = CreateSearchTools();

        await tools.SearchSkills("test", outputMode: "full", sessionId: "my-session");

        Assert.True(_sessionTracker.IsLoaded("test-skill", "my-session"));
        Assert.False(_sessionTracker.IsLoaded("test-skill")); // default session
    }

    [Fact]
    public async Task LoadSkill_PassesSessionIdToMarkLoaded()
    {
        _repository.GetByNameAsync("test-skill", Arg.Any<CancellationToken>()).Returns(SampleSkill);
        var tools = CreateSearchTools();

        await tools.LoadSkill("test-skill", sessionId: "sess-42");

        Assert.True(_sessionTracker.IsLoaded("test-skill", "sess-42"));
        Assert.False(_sessionTracker.IsLoaded("test-skill")); // default
    }

    // --- reset-session tool tests ---

    [Fact]
    public void ResetSession_DefaultSession_ReturnsConfirmation()
    {
        _sessionTracker.MarkLoaded("skill-a");
        var tools = CreateSessionTools();

        var result = tools.ResetSession();

        Assert.Equal("Session reset successfully.", result);
        Assert.False(_sessionTracker.IsLoaded("skill-a"));
    }

    [Fact]
    public void ResetSession_NamedSession_ReturnsConfirmation()
    {
        _sessionTracker.MarkLoaded("skill-a", "my-sess");
        var tools = CreateSessionTools();

        var result = tools.ResetSession("my-sess");

        Assert.Equal("Session 'my-sess' reset successfully.", result);
        Assert.False(_sessionTracker.IsLoaded("skill-a", "my-sess"));
    }

    [Fact]
    public void ResetSession_CallsResetOnSessionTracker()
    {
        _sessionTracker.MarkLoaded("s1");
        _sessionTracker.MarkLoaded("s2", "other");
        var tools = CreateSessionTools();

        tools.ResetSession();

        // Only default is reset
        Assert.Empty(_sessionTracker.GetLoadedSkills());
        Assert.Single(_sessionTracker.GetLoadedSkills("other"));
    }
}
