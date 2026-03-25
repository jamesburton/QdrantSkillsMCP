using Xunit;
using QdrantSkillsMCP.Infrastructure.Session;

namespace QdrantSkillsMCP.UnitTests.Session;

/// <summary>
/// Tests for keyed session support in <see cref="InMemorySessionTracker"/>.
/// Default (null sessionId) tests validate backward-compatibility with Phase 1 behavior.
/// </summary>
public sealed class KeyedSessionTrackerTests
{
    [Fact]
    public void DefaultSession_MarkAndIsLoaded_WorksLikePhase1()
    {
        var tracker = new InMemorySessionTracker();

        tracker.MarkLoaded("skill-a");

        Assert.True(tracker.IsLoaded("skill-a"));
        Assert.False(tracker.IsLoaded("skill-b"));
    }

    [Fact]
    public void DefaultSession_GetLoadedSkills_ReturnsSorted()
    {
        var tracker = new InMemorySessionTracker();

        tracker.MarkLoaded("c");
        tracker.MarkLoaded("a");
        tracker.MarkLoaded("b");

        var loaded = tracker.GetLoadedSkills();
        Assert.Equal(["a", "b", "c"], loaded);
    }

    [Fact]
    public void NamedSessions_TrackIndependently()
    {
        var tracker = new InMemorySessionTracker();

        tracker.MarkLoaded("skill-a", "session-1");
        tracker.MarkLoaded("skill-b", "session-2");

        Assert.True(tracker.IsLoaded("skill-a", "session-1"));
        Assert.False(tracker.IsLoaded("skill-a", "session-2"));

        Assert.True(tracker.IsLoaded("skill-b", "session-2"));
        Assert.False(tracker.IsLoaded("skill-b", "session-1"));
    }

    [Fact]
    public void ResetNull_ClearsDefaultSession_NotNamedSessions()
    {
        var tracker = new InMemorySessionTracker();

        tracker.MarkLoaded("skill-a"); // default
        tracker.MarkLoaded("skill-b", "session-1");

        tracker.Reset(); // reset default only

        Assert.False(tracker.IsLoaded("skill-a"));
        Assert.True(tracker.IsLoaded("skill-b", "session-1"));
    }

    [Fact]
    public void ResetNamedSession_ClearsOnlyThatSession()
    {
        var tracker = new InMemorySessionTracker();

        tracker.MarkLoaded("skill-a", "session-1");
        tracker.MarkLoaded("skill-b", "session-2");
        tracker.MarkLoaded("skill-c"); // default

        tracker.Reset("session-1");

        Assert.False(tracker.IsLoaded("skill-a", "session-1"));
        Assert.True(tracker.IsLoaded("skill-b", "session-2"));
        Assert.True(tracker.IsLoaded("skill-c"));
    }

    [Fact]
    public void IsLoaded_AcrossSessions_Isolated()
    {
        var tracker = new InMemorySessionTracker();

        tracker.MarkLoaded("shared-name", "session-a");

        Assert.True(tracker.IsLoaded("shared-name", "session-a"));
        Assert.False(tracker.IsLoaded("shared-name", "session-b"));
        Assert.False(tracker.IsLoaded("shared-name")); // default
    }

    [Fact]
    public void GetLoadedSkills_PerSession_ReturnsSorted()
    {
        var tracker = new InMemorySessionTracker();

        tracker.MarkLoaded("z", "s1");
        tracker.MarkLoaded("a", "s1");
        tracker.MarkLoaded("m", "s1");
        tracker.MarkLoaded("only-default");

        var s1Skills = tracker.GetLoadedSkills("s1");
        Assert.Equal(["a", "m", "z"], s1Skills);

        var defaultSkills = tracker.GetLoadedSkills();
        Assert.Equal(["only-default"], defaultSkills);
    }

    [Fact]
    public void GetLoadedSkills_NonExistentSession_ReturnsEmpty()
    {
        var tracker = new InMemorySessionTracker();

        var skills = tracker.GetLoadedSkills("never-created");
        Assert.Empty(skills);
    }

    [Fact]
    public void IsLoaded_NonExistentSession_ReturnsFalse()
    {
        var tracker = new InMemorySessionTracker();

        Assert.False(tracker.IsLoaded("anything", "never-created"));
    }

    [Fact]
    public void Reset_NonExistentSession_DoesNotThrow()
    {
        var tracker = new InMemorySessionTracker();

        var ex = Record.Exception(() => tracker.Reset("never-created"));
        Assert.Null(ex);
    }

    [Fact]
    public void CaseInsensitive_SkillNames_WithinSession()
    {
        var tracker = new InMemorySessionTracker();

        tracker.MarkLoaded("My-Skill", "s1");

        Assert.True(tracker.IsLoaded("my-skill", "s1"));
        Assert.True(tracker.IsLoaded("MY-SKILL", "s1"));
    }
}
