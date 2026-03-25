using System.Text.RegularExpressions;

namespace QdrantSkillsMCP.Core.Validation;

/// <summary>
/// Validates skill names against the naming convention:
/// lowercase letters, numbers, and hyphens only; max 64 characters;
/// cannot start or end with a hyphen; cannot be empty.
/// </summary>
public static partial class SkillNameValidator
{
    private const int MaxLength = 64;

    // Handles 1-char names (single alphanumeric) and 2+ char names (bookended by alphanumeric).
    [GeneratedRegex(@"^[a-z0-9]([a-z0-9-]{0,62}[a-z0-9])?$")]
    private static partial Regex NamePattern();

    /// <summary>
    /// Validates a skill name.
    /// </summary>
    /// <returns>A tuple indicating validity and an optional error message.</returns>
    public static (bool IsValid, string? Error) Validate(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return (false, "Skill name cannot be empty.");

        if (name.Length > MaxLength)
            return (false, $"Skill name cannot exceed {MaxLength} characters (got {name.Length}).");

        if (!NamePattern().IsMatch(name))
            return (false, "Skill name must contain only lowercase letters, numbers, and hyphens, and cannot start or end with a hyphen.");

        return (true, null);
    }
}
