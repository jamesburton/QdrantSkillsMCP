using Xunit;
using QdrantSkillsMCP.Infrastructure.Session;

namespace QdrantSkillsMCP.UnitTests.Session;

public sealed class InMemorySessionTrackerTests
{
    [Fact]
    public void MarkLoaded_ThenIsLoaded_ReturnsTrue()
    {
        var tracker = new InMemorySessionTracker();

        tracker.MarkLoaded("skill-a");

        Assert.True(tracker.IsLoaded("skill-a"));
    }

    [Fact]
    public void IsLoaded_UnknownSkill_ReturnsFalse()
    {
        var tracker = new InMemorySessionTracker();

        Assert.False(tracker.IsLoaded("unknown"));
    }

    [Fact]
    public void GetLoadedSkills_ReturnsSorted()
    {
        var tracker = new InMemorySessionTracker();

        tracker.MarkLoaded("b");
        tracker.MarkLoaded("a");
        tracker.MarkLoaded("c");

        var loaded = tracker.GetLoadedSkills();

        Assert.Equal(3, loaded.Count);
        Assert.Equal("a", loaded[0]);
        Assert.Equal("b", loaded[1]);
        Assert.Equal("c", loaded[2]);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var tracker = new InMemorySessionTracker();
        tracker.MarkLoaded("skill-a");
        tracker.MarkLoaded("skill-b");

        tracker.Reset();

        Assert.Empty(tracker.GetLoadedSkills());
        Assert.False(tracker.IsLoaded("skill-a"));
        Assert.False(tracker.IsLoaded("skill-b"));
    }

    [Fact]
    public void MarkLoaded_Duplicate_NoErrorAndSingleEntry()
    {
        var tracker = new InMemorySessionTracker();

        tracker.MarkLoaded("skill-a");
        tracker.MarkLoaded("skill-a");

        Assert.Single(tracker.GetLoadedSkills());
        Assert.True(tracker.IsLoaded("skill-a"));
    }

    [Fact]
    public void ThreadSafety_ConcurrentMarkLoaded_NoExceptionsAllTracked()
    {
        var tracker = new InMemorySessionTracker();
        var skillNames = Enumerable.Range(0, 100).Select(i => $"skill-{i:D3}").ToArray();

        Parallel.ForEach(skillNames, name => tracker.MarkLoaded(name));

        var loaded = tracker.GetLoadedSkills();
        Assert.Equal(100, loaded.Count);
        foreach (var name in skillNames)
        {
            Assert.True(tracker.IsLoaded(name), $"Expected '{name}' to be loaded");
        }
    }

    [Fact]
    public void IsLoaded_CaseInsensitive()
    {
        // InMemorySessionTracker uses StringComparer.OrdinalIgnoreCase
        var tracker = new InMemorySessionTracker();
        tracker.MarkLoaded("My-Skill");

        Assert.True(tracker.IsLoaded("my-skill"));
        Assert.True(tracker.IsLoaded("MY-SKILL"));
    }
}
