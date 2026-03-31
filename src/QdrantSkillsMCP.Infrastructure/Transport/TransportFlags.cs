namespace QdrantSkillsMCP.Infrastructure.Transport;

/// <summary>
/// Parses and validates transport-related CLI flags.
/// </summary>
internal static class TransportFlags
{
    public static bool WantsHttp(string[] args)
        => args.Contains("--http") || args.Any(a => a == "--url" || a.StartsWith("--url="));

    public static bool WantsStdio(string[] args)
        => args.Contains("--stdio");

    public static bool HasConflict(string[] args)
        => WantsHttp(args) && WantsStdio(args);

    /// <summary>
    /// Resolves listen URL from: --url flag > QDRANT_SKILLS_URL env > config > default.
    /// Per D-06 precedence order.
    /// </summary>
    public static string ResolveListenUrl(string[] args, string? envUrl = null, string? configUrl = null)
    {
        // 1. --url flag (highest priority) -- supports --url VALUE and --url=VALUE
        var urlIndex = Array.IndexOf(args, "--url");
        if (urlIndex >= 0 && urlIndex + 1 < args.Length)
            return args[urlIndex + 1];

        var urlEquals = args.FirstOrDefault(a => a.StartsWith("--url="));
        if (urlEquals is not null)
            return urlEquals["--url=".Length..];

        // 2. QDRANT_SKILLS_URL env var
        if (!string.IsNullOrEmpty(envUrl))
            return envUrl;

        // 3. Config system (project > user)
        if (!string.IsNullOrEmpty(configUrl))
            return configUrl;

        // 4. Default per D-05
        return "http://localhost:8080";
    }

    /// <summary>
    /// Strips custom transport flags from args before passing to WebApplication.CreateBuilder.
    /// Removes: --http, --url VALUE, --url=VALUE, --stdio
    /// </summary>
    public static string[] StripTransportFlags(string[] args)
    {
        var result = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--http" || args[i] == "--stdio")
                continue;
            if (args[i] == "--url" && i + 1 < args.Length)
            {
                i++; // skip value too
                continue;
            }
            if (args[i].StartsWith("--url="))
                continue;
            result.Add(args[i]);
        }
        return result.ToArray();
    }
}
