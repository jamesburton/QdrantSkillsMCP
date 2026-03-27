namespace QdrantSkillsMCP.Infrastructure.Configuration;

/// <summary>
/// Utility for masking API keys and identifying secret config keys.
/// </summary>
public static class SecretMask
{
    /// <summary>
    /// Masks a secret value showing first 3 and last 4 characters.
    /// Returns "(not set)" for null/empty, "****" for short values (8 chars or fewer).
    /// </summary>
    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "(not set)";

        if (value.Length <= 8)
            return "****";

        return $"{value[..3]}****{value[^4..]}";
    }

    /// <summary>
    /// Returns true if the key name indicates a secret value (contains "ApiKey" or "Secret").
    /// </summary>
    public static bool IsSecret(string key)
        => key.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)
        || key.Contains("Secret", StringComparison.OrdinalIgnoreCase);
}
