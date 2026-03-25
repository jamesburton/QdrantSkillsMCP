using Xunit;
using QdrantSkillsMCP.Infrastructure.Yaml;

namespace QdrantSkillsMCP.UnitTests.Yaml;

public sealed class SkillParserTests
{
    private readonly SkillParser _parser = new();

    private const string StandardSkillContent =
        """
        ---
        name: my-skill
        description: A test skill
        tags:
          - csharp
          - testing
        ---
        # My Skill

        This is the body.
        """;

    [Fact]
    public void Parse_StandardSkill_ExtractsAllFields()
    {
        var (metadata, body, raw) = _parser.Parse(StandardSkillContent);

        Assert.Equal("my-skill", metadata.Name);
        Assert.Equal("A test skill", metadata.Description);
        Assert.NotNull(metadata.Tags);
        Assert.Equal(2, metadata.Tags!.Count);
        Assert.Contains("csharp", metadata.Tags);
        Assert.Contains("testing", metadata.Tags);
        Assert.Contains("# My Skill", body);
        Assert.Contains("This is the body.", body);
    }

    [Fact]
    public void Parse_RoundTrip_RawContentIdenticalToInput()
    {
        var (_, _, raw) = _parser.Parse(StandardSkillContent);

        Assert.Equal(StandardSkillContent, raw);
    }

    [Fact]
    public void Parse_MissingFrontmatter_TreatedAsBodyOnly()
    {
        const string plainMarkdown = "# Just Markdown\n\nNo frontmatter here.";

        var (metadata, body, raw) = _parser.Parse(plainMarkdown);

        Assert.Null(metadata.Name);
        Assert.Null(metadata.Description);
        Assert.Equal(plainMarkdown, body);
        Assert.Equal(plainMarkdown, raw);
    }

    [Fact]
    public void Parse_EmptyBody_BodyIsEmptyString()
    {
        const string frontmatterOnly = "---\nname: empty-body\n---\n";

        var (metadata, body, _) = _parser.Parse(frontmatterOnly);

        Assert.Equal("empty-body", metadata.Name);
        Assert.Equal(string.Empty, body);
    }

    [Fact]
    public void Parse_MultilineDescription_ParsedCorrectly()
    {
        const string content =
            """
            ---
            name: multiline-desc
            description: >
              This is a long description
              that spans multiple lines.
            ---
            Body here.
            """;

        var (metadata, _, _) = _parser.Parse(content);

        Assert.Equal("multiline-desc", metadata.Name);
        Assert.NotNull(metadata.Description);
        Assert.Contains("long description", metadata.Description!);
        Assert.Contains("multiple lines", metadata.Description!);
    }

    [Fact]
    public void Parse_SpecialCharactersInBody_NotCorrupted()
    {
        const string content =
            """
            ---
            name: special-chars
            ---
            Code: `var x = 1;`
            Table: | Col1 | Col2 |
            Brackets: [link](url) {braces}
            """;

        var (_, body, raw) = _parser.Parse(content);

        Assert.Contains("`var x = 1;`", body);
        Assert.Contains("| Col1 | Col2 |", body);
        Assert.Contains("[link](url) {braces}", body);
        Assert.Equal(content, raw);
    }

    [Fact]
    public void Parse_ExtraFrontmatterField_Preserved()
    {
        // The SkillFrontmatter has an "extra" key for capturing additional data.
        // Unknown YAML keys outside of "extra" are dropped by IgnoreUnmatchedProperties.
        // This test validates the "extra" field mechanism works.
        const string content =
            """
            ---
            name: extra-fields
            extra:
              custom_key: custom_value
            ---
            Body.
            """;

        var (metadata, _, _) = _parser.Parse(content);

        Assert.Equal("extra-fields", metadata.Name);
        Assert.NotNull(metadata.ExtraFields);
        Assert.True(metadata.ExtraFields!.ContainsKey("custom_key"));
        Assert.Equal("custom_value", metadata.ExtraFields["custom_key"].ToString());
    }

    [Fact]
    public void ToSkill_StandardContent_CreatesSkillWithAllFields()
    {
        var skill = _parser.ToSkill(StandardSkillContent);

        Assert.Equal("my-skill", skill.Name);
        Assert.Equal("A test skill", skill.Description);
        Assert.NotNull(skill.Tags);
        Assert.Contains("csharp", skill.Tags!);
        Assert.Equal(StandardSkillContent, skill.RawContent);
        Assert.False(skill.Archived);
    }

    [Fact]
    public void Parse_NullContent_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _parser.Parse(null!));
    }
}
