namespace QdrantSkillsMCP.Infrastructure.SkillGuide;

/// <summary>
/// Loads and merges FrequentSkills from user-level and project-level files.
/// Merge order (most specific wins): user shared -> user local -> project shared -> project local.
/// </summary>
public sealed class FrequentSkillsService
{
    private readonly string _userDir;

    /// <summary>
    /// Creates a new FrequentSkillsService.
    /// </summary>
    /// <param name="userDir">
    /// User-level directory for FrequentSkills files.
    /// Defaults to ~/.qdrant-skills/ if null.
    /// </param>
    public FrequentSkillsService(string? userDir = null)
    {
        _userDir = userDir
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qdrant-skills");
    }

    /// <summary>
    /// Loads frequent skill names from all available FrequentSkills files,
    /// merging with later files overriding earlier ones for the same skill name.
    /// </summary>
    /// <param name="projectRoot">Optional project root directory for project-level files.</param>
    /// <returns>Deduplicated list of skill names.</returns>
    public IReadOnlyList<string> LoadFrequentSkills(string? projectRoot = null)
    {
        // Ordered from least specific to most specific.
        // Later entries override earlier entries for the same skill name.
        var filePaths = new List<string>
        {
            Path.Combine(_userDir, "FrequentSkills.md"),
            Path.Combine(_userDir, "FrequentSkills.local.md"),
        };

        if (projectRoot is not null)
        {
            filePaths.Add(Path.Combine(projectRoot, "FrequentSkills.md"));
            filePaths.Add(Path.Combine(projectRoot, "FrequentSkills.local.md"));
        }

        // Use a dictionary to deduplicate (last wins for same name).
        var skills = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in filePaths)
        {
            if (!File.Exists(path))
                continue;

            var names = ParseSkillNames(File.ReadAllText(path));
            foreach (var name in names)
            {
                skills[name] = name; // Later file overwrites earlier for same key
            }
        }

        return skills.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Parses skill names from markdown content.
    /// Recognizes lines starting with "- " or "* " as skill name entries.
    /// </summary>
    internal static IEnumerable<string> ParseSkillNames(string content)
    {
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();

            // Match "- skill-name" or "* skill-name"
            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                var name = trimmed[2..].Trim();
                if (name.Length > 0 && !name.StartsWith("<!--"))
                    yield return name;
            }
            else if (trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                var name = trimmed[2..].Trim();
                if (name.Length > 0 && !name.StartsWith("<!--"))
                    yield return name;
            }
        }
    }
}
