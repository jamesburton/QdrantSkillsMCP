using QdrantSkillsMCP.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace QdrantSkillsMCP.Infrastructure.Yaml;

/// <summary>
/// Stateless parser for markdown documents with YAML frontmatter.
/// Splits on <c>---</c> delimiters and deserializes the YAML block into <see cref="SkillFrontmatter"/>.
/// </summary>
public sealed class SkillParser
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Parses raw markdown+frontmatter content into structured components.
    /// </summary>
    /// <param name="rawContent">The complete markdown document including optional YAML frontmatter.</param>
    /// <returns>Parsed frontmatter metadata, the markdown body (frontmatter stripped), and the original raw content.</returns>
    public (SkillFrontmatter Metadata, string MarkdownBody, string RawContent) Parse(string rawContent)
    {
        ArgumentNullException.ThrowIfNull(rawContent);

        var (yamlBlock, markdownBody) = SplitFrontmatter(rawContent);

        SkillFrontmatter metadata;
        if (string.IsNullOrWhiteSpace(yamlBlock))
        {
            metadata = new SkillFrontmatter();
        }
        else
        {
            try
            {
                metadata = YamlDeserializer.Deserialize<SkillFrontmatter>(yamlBlock) ?? new SkillFrontmatter();
            }
            catch (YamlDotNet.Core.YamlException)
            {
                // If YAML is malformed, treat entire content as markdown body with no metadata
                metadata = new SkillFrontmatter();
                markdownBody = rawContent;
            }
        }

        return (metadata, markdownBody, rawContent);
    }

    /// <summary>
    /// Convenience method that parses raw content and returns a <see cref="Skill"/> model.
    /// The <paramref name="rawContent"/> is stored as-is for lossless round-trip retrieval (CRUD-05).
    /// </summary>
    /// <param name="rawContent">The complete markdown document including optional YAML frontmatter.</param>
    /// <returns>A fully populated Skill record.</returns>
    public Skill ToSkill(string rawContent)
    {
        var (metadata, markdownBody, raw) = Parse(rawContent);

        return new Skill
        {
            Name = metadata.Name ?? string.Empty,
            Description = metadata.Description,
            Tags = metadata.Tags?.ToArray(),
            RawContent = raw,
            MarkdownBody = markdownBody,
            UpdatedAt = DateTimeOffset.UtcNow,
            Archived = false
        };
    }

    /// <summary>
    /// Splits a markdown document into its YAML frontmatter block and markdown body.
    /// Frontmatter is delimited by <c>---</c> on its own line at the start of the document.
    /// </summary>
    private static (string YamlBlock, string MarkdownBody) SplitFrontmatter(string content)
    {
        const string delimiter = "---";

        var trimmed = content.TrimStart();
        if (!trimmed.StartsWith(delimiter))
        {
            // No frontmatter — entire content is the markdown body
            return (string.Empty, content);
        }

        // Find end of opening delimiter line
        var firstDelimiterEnd = trimmed.IndexOf('\n');
        if (firstDelimiterEnd < 0)
        {
            // Only "---" with no newline — treat as frontmatter-only with empty body
            return (string.Empty, string.Empty);
        }

        // Find the closing "---" delimiter
        var searchStart = firstDelimiterEnd + 1;
        var closingIndex = -1;

        while (searchStart < trimmed.Length)
        {
            var lineEnd = trimmed.IndexOf('\n', searchStart);
            var line = lineEnd >= 0
                ? trimmed[searchStart..lineEnd].Trim()
                : trimmed[searchStart..].Trim();

            if (line == delimiter)
            {
                closingIndex = searchStart;
                break;
            }

            if (lineEnd < 0)
                break;

            searchStart = lineEnd + 1;
        }

        if (closingIndex < 0)
        {
            // No closing delimiter found — treat entire content as markdown body
            return (string.Empty, content);
        }

        var yamlBlock = trimmed[(firstDelimiterEnd + 1)..closingIndex].Trim();

        // Markdown body starts after the closing delimiter line
        var bodyStart = trimmed.IndexOf('\n', closingIndex);
        var markdownBody = bodyStart >= 0
            ? trimmed[(bodyStart + 1)..]
            : string.Empty;

        return (yamlBlock, markdownBody);
    }
}

/// <summary>
/// Represents the structured YAML frontmatter of a skill document.
/// Unknown keys are captured in <see cref="ExtraFields"/> to prevent data loss.
/// </summary>
public sealed class SkillFrontmatter
{
    /// <summary>Skill name (primary key).</summary>
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    /// <summary>Short description of the skill.</summary>
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    /// <summary>Categorization tags.</summary>
    [YamlMember(Alias = "tags")]
    public List<string>? Tags { get; set; }

    /// <summary>Skill version string.</summary>
    [YamlMember(Alias = "version")]
    public string? Version { get; set; }

    /// <summary>
    /// Captures the literal YAML key <c>extra:</c> from the frontmatter.
    /// This does NOT capture arbitrary unknown keys — YamlDotNet maps only the exact alias "extra".
    /// Unknown frontmatter keys are silently ignored (via <c>IgnoreUnmatchedProperties</c>).
    /// </summary>
    [YamlMember(Alias = "extra")]
    public Dictionary<string, object>? ExtraFields { get; set; }
}
