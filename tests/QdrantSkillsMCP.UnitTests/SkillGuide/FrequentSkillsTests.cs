using QdrantSkillsMCP.Infrastructure.SkillGuide;
using Xunit;

namespace QdrantSkillsMCP.UnitTests.SkillGuide;

/// <summary>
/// Tests for FrequentSkillsService dual-file merge system.
/// </summary>
public sealed class FrequentSkillsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _userDir;
    private readonly string _projectDir;

    public FrequentSkillsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"freq-skills-{Guid.NewGuid():N}");
        _userDir = Path.Combine(_tempDir, "user");
        _projectDir = Path.Combine(_tempDir, "project");
        Directory.CreateDirectory(_userDir);
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void NoFiles_ReturnsEmptyList()
    {
        var svc = new FrequentSkillsService(_userDir);
        var result = svc.LoadFrequentSkills(_projectDir);

        Assert.Empty(result);
    }

    [Fact]
    public void UserLevelOnly_ReturnThoseSkills()
    {
        File.WriteAllText(Path.Combine(_userDir, "FrequentSkills.md"),
            "# Frequent Skills\n- coding-standards\n- error-handling\n");

        var svc = new FrequentSkillsService(_userDir);
        var result = svc.LoadFrequentSkills(_projectDir);

        Assert.Equal(2, result.Count);
        Assert.Contains("coding-standards", result);
        Assert.Contains("error-handling", result);
    }

    [Fact]
    public void ProjectLevel_OverridesUserLevel_ForSameName()
    {
        File.WriteAllText(Path.Combine(_userDir, "FrequentSkills.md"),
            "- skill-a\n- skill-b\n");
        File.WriteAllText(Path.Combine(_projectDir, "FrequentSkills.md"),
            "- skill-b\n- skill-c\n");

        var svc = new FrequentSkillsService(_userDir);
        var result = svc.LoadFrequentSkills(_projectDir);

        // skill-b appears once (project overrides user), plus skill-a and skill-c
        Assert.Equal(3, result.Count);
        Assert.Contains("skill-a", result);
        Assert.Contains("skill-b", result);
        Assert.Contains("skill-c", result);
    }

    [Fact]
    public void ProjectLocal_OverridesProjectLevel_ForSameName()
    {
        File.WriteAllText(Path.Combine(_projectDir, "FrequentSkills.md"),
            "- skill-x\n- skill-y\n");
        File.WriteAllText(Path.Combine(_projectDir, "FrequentSkills.local.md"),
            "- skill-y\n- skill-z\n");

        var svc = new FrequentSkillsService(_userDir);
        var result = svc.LoadFrequentSkills(_projectDir);

        Assert.Equal(3, result.Count);
        Assert.Contains("skill-x", result);
        Assert.Contains("skill-y", result);
        Assert.Contains("skill-z", result);
    }

    [Fact]
    public void AllFourTiers_MergeCorrectly_WithDeduplication()
    {
        // User-level shared
        File.WriteAllText(Path.Combine(_userDir, "FrequentSkills.md"),
            "- base-skill\n- override-me\n");
        // User-level local
        File.WriteAllText(Path.Combine(_userDir, "FrequentSkills.local.md"),
            "- user-personal\n- override-me\n");
        // Project-level shared
        File.WriteAllText(Path.Combine(_projectDir, "FrequentSkills.md"),
            "- project-skill\n- override-me\n");
        // Project-level local
        File.WriteAllText(Path.Combine(_projectDir, "FrequentSkills.local.md"),
            "- project-personal\n- override-me\n");

        var svc = new FrequentSkillsService(_userDir);
        var result = svc.LoadFrequentSkills(_projectDir);

        // override-me appears only once (deduplicated)
        Assert.Contains("base-skill", result);
        Assert.Contains("user-personal", result);
        Assert.Contains("project-skill", result);
        Assert.Contains("project-personal", result);
        Assert.Contains("override-me", result);
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void MalformedFile_NoListItems_ReturnsEmptyList()
    {
        File.WriteAllText(Path.Combine(_userDir, "FrequentSkills.md"),
            "# Just a heading\nSome paragraph text.\n");

        var svc = new FrequentSkillsService(_userDir);
        var result = svc.LoadFrequentSkills(_projectDir);

        Assert.Empty(result);
    }

    [Fact]
    public void StarBullets_AlsoWork()
    {
        File.WriteAllText(Path.Combine(_userDir, "FrequentSkills.md"),
            "* star-skill\n- dash-skill\n");

        var svc = new FrequentSkillsService(_userDir);
        var result = svc.LoadFrequentSkills(_projectDir);

        Assert.Equal(2, result.Count);
        Assert.Contains("star-skill", result);
        Assert.Contains("dash-skill", result);
    }
}
