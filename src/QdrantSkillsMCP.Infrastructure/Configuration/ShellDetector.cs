using System.Text;

namespace QdrantSkillsMCP.Infrastructure.Configuration;

/// <summary>
/// Shell types supported for env var template generation.
/// </summary>
public enum ShellType { Bash, Zsh, PowerShell, Cmd }

/// <summary>
/// Detects the current shell environment and generates env var templates.
/// </summary>
public static class ShellDetector
{
    /// <summary>
    /// Detects the current shell using environment variables and OS detection.
    /// </summary>
    public static ShellType DetectShell()
        => DetectShell(Environment.GetEnvironmentVariable, OperatingSystem.IsWindows());

    /// <summary>
    /// Testable overload for shell detection.
    /// </summary>
    public static ShellType DetectShell(Func<string, string?> getEnvVar, bool isWindows)
    {
        // PowerShell sets PSModulePath
        if (getEnvVar("PSModulePath") is not null)
            return ShellType.PowerShell;

        // Unix SHELL env var
        var shell = getEnvVar("SHELL");
        if (shell is not null)
        {
            if (shell.EndsWith("/bash", StringComparison.Ordinal))
                return ShellType.Bash;
            if (shell.EndsWith("/zsh", StringComparison.Ordinal))
                return ShellType.Zsh;
        }

        // Windows fallback
        if (isWindows)
            return ShellType.Cmd;

        // Ultimate fallback
        return ShellType.Bash;
    }

    /// <summary>
    /// Generates an env var template for all configurable keys.
    /// Keys with non-null values in <paramref name="currentValues"/> are uncommented;
    /// missing/null values are commented out.
    /// </summary>
    public static string GenerateEnvTemplate(ShellType shell, Dictionary<string, string?> currentValues)
    {
        var sb = new StringBuilder();

        foreach (var key in ConfigManager.ConfigurableKeys)
        {
            currentValues.TryGetValue(key, out var value);
            var hasValue = value is not null;
            var envKey = $"QDRANT_SKILLS__{key}";

            var line = shell switch
            {
                ShellType.Bash or ShellType.Zsh => hasValue
                    ? $"export {envKey}=\"{value}\""
                    : $"# export {envKey}=\"\"",

                ShellType.PowerShell => hasValue
                    ? $"$env:{envKey} = \"{value}\""
                    : $"# $env:{envKey} = \"\"",

                ShellType.Cmd => hasValue
                    ? $"set {envKey}={value}"
                    : $"REM set {envKey}=",

                _ => $"# {envKey}={value ?? ""}"
            };

            sb.AppendLine(line);
        }

        return sb.ToString();
    }
}
