using System.Reflection;
using QdrantSkillsMCP.Infrastructure.Tools;
using Xunit;

namespace QdrantSkillsMCP.UnitTests.SkillGuide;

/// <summary>
/// Tests for SKILL.md embedded resource and the get-skill-guide MCP tool.
/// </summary>
public sealed class SkillGuideTests
{
    private static readonly Assembly InfraAssembly =
        typeof(SkillGuideTools).Assembly;

    [Fact]
    public void SkillMd_EmbeddedResource_Exists_And_NonEmpty()
    {
        using var stream = InfraAssembly.GetManifestResourceStream(
            "QdrantSkillsMCP.Infrastructure.SkillGuide.SKILL.md");

        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.NotEmpty(content);
    }

    [Fact]
    public void SkillMd_Contains_SearchSkills_ToolReference()
    {
        using var stream = InfraAssembly.GetManifestResourceStream(
            "QdrantSkillsMCP.Infrastructure.SkillGuide.SKILL.md");

        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.Contains("search-skills", content);
    }

    [Fact]
    public void EnableSkillSearch_EmbeddedResource_Exists_And_NonEmpty()
    {
        using var stream = InfraAssembly.GetManifestResourceStream(
            "QdrantSkillsMCP.Infrastructure.SkillGuide.EnableSkillSearch.md");

        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Assert.NotEmpty(content);
    }

    [Fact]
    public void GetSkillGuide_Returns_NonEmpty_String()
    {
        var tools = new SkillGuideTools();
        var result = tools.GetSkillGuide();

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void GetSkillGuide_Contains_SearchBeforeLoad_Pattern()
    {
        var tools = new SkillGuideTools();
        var result = tools.GetSkillGuide();

        Assert.Contains("search-skills", result);
        Assert.Contains("load-skill", result);
    }
}
