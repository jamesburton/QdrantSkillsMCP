using Spectre.Console;
using Spectre.Console.Rendering;
using QdrantSkillsMCP.Infrastructure.Configuration;

namespace QdrantSkillsMCP.Infrastructure.Cli.Commands;

/// <summary>
/// CLI command dispatcher for all --config operations.
/// Subcommands: show, set, get, init, reset, use, env, validate.
/// No subcommand enters interactive wizard.
/// </summary>
public static class ConfigCommand
{
    /// <summary>
    /// Dispatches to the appropriate config subcommand based on args.
    /// </summary>
    public static async Task<int> RunAsync(ConfigManager configManager, string[] args)
    {
        // Find the subcommand: first arg after "--config"
        var configIndex = Array.IndexOf(args, "--config");
        var subcommand = (configIndex >= 0 && configIndex + 1 < args.Length)
            ? args[configIndex + 1]
            : null;

        // Skip subcommands that look like flags
        if (subcommand is not null && subcommand.StartsWith('-'))
            subcommand = null;

        return subcommand switch
        {
            "show" => RunShow(configManager, args),
            "set" => await RunSet(configManager, args),
            "get" => RunGet(configManager, args),
            "init" => await RunInit(configManager),
            "reset" => await RunReset(configManager, args),
            "use" => await RunUse(configManager, args),
            "env" => RunEnv(configManager),
            "validate" => await RunValidate(configManager),
            null => await RunInteractiveAsync(configManager),
            _ => RunUsage()
        };
    }

    private static int RunShow(ConfigManager configManager, string[] args)
    {
        var reveal = args.Contains("--reveal");
        var entries = configManager.GetAllWithSources();

        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(Console.Out)
        });

        var table = new Table();
        table.AddColumn("Key");
        table.AddColumn("Value");
        table.AddColumn("Source");

        foreach (var (key, entry) in entries)
        {
            var displayValue = entry.Value;
            if (!reveal && SecretMask.IsSecret(key))
            {
                displayValue = SecretMask.Mask(entry.Value);
            }

            table.AddRow(
                Markup.Escape(key),
                Markup.Escape(displayValue ?? "(not set)"),
                Markup.Escape(entry.Source));
        }

        console.Write(table);
        return 0;
    }

    private static async Task<int> RunSet(ConfigManager configManager, string[] args)
    {
        // Find key=value arg after "set"
        var setIndex = Array.IndexOf(args, "set");
        if (setIndex < 0 || setIndex + 1 >= args.Length)
        {
            Console.Error.WriteLine("Usage: --config set <key>=<value> [--project]");
            return 1;
        }

        var keyValue = args[setIndex + 1];
        var eqIndex = keyValue.IndexOf('=');
        if (eqIndex <= 0)
        {
            Console.Error.WriteLine("Usage: --config set <key>=<value> [--project]");
            return 1;
        }

        var key = keyValue[..eqIndex];
        var value = keyValue[(eqIndex + 1)..];
        var projectScope = args.Contains("--project");

        await configManager.SetValueAsync(key, value, projectScope);
        Console.WriteLine($"Set {key} = {value}{(projectScope ? " [project]" : " [user]")}");
        return 0;
    }

    private static int RunGet(ConfigManager configManager, string[] args)
    {
        var getIndex = Array.IndexOf(args, "get");
        if (getIndex < 0 || getIndex + 1 >= args.Length)
        {
            Console.Error.WriteLine("Usage: --config get <key>");
            return 1;
        }

        var key = args[getIndex + 1];
        var value = configManager.GetValue(key);
        Console.WriteLine(value ?? "(not set)");
        return 0;
    }

    private static async Task<int> RunInit(ConfigManager configManager)
    {
        await configManager.InitAsync();
        Console.WriteLine("Initialized config with local preset.");
        return 0;
    }

    private static async Task<int> RunReset(ConfigManager configManager, string[] args)
    {
        var resetIndex = Array.IndexOf(args, "reset");
        string? key = null;

        if (resetIndex >= 0 && resetIndex + 1 < args.Length)
        {
            var nextArg = args[resetIndex + 1];
            if (!nextArg.StartsWith('-'))
                key = nextArg;
        }

        await configManager.ResetAsync(key);
        Console.WriteLine(key is not null ? $"Reset {key}" : "Reset all config values");
        return 0;
    }

    private static async Task<int> RunUse(ConfigManager configManager, string[] args)
    {
        var useIndex = Array.IndexOf(args, "use");
        if (useIndex < 0 || useIndex + 1 >= args.Length)
        {
            Console.Error.WriteLine("Usage: --config use <profile-name>");
            return 1;
        }

        var profileName = args[useIndex + 1];
        await configManager.UseProfileAsync(profileName);
        Console.WriteLine($"Switched to profile: {profileName}");
        return 0;
    }

    private static int RunEnv(ConfigManager configManager)
    {
        var shell = ShellDetector.DetectShell();
        var entries = configManager.GetAllWithSources();
        var currentValues = new Dictionary<string, string?>();
        foreach (var (key, entry) in entries)
        {
            currentValues[key] = entry.Value;
        }

        var template = ShellDetector.GenerateEnvTemplate(shell, currentValues);
        Console.Write(template);
        return 0;
    }

    /// <summary>
    /// Validates Qdrant connectivity and embedding provider.
    /// Implemented in Task 2.
    /// </summary>
    internal static async Task<int> RunValidate(ConfigManager configManager)
    {
        // Placeholder -- will be implemented in Task 2
        Console.WriteLine("validate: not yet implemented");
        return 1;
    }

    /// <summary>
    /// Interactive wizard loop. Implemented in Task 2.
    /// </summary>
    internal static async Task<int> RunInteractiveAsync(ConfigManager configManager)
    {
        // Placeholder -- will be implemented in Task 2
        Console.WriteLine("Interactive wizard: not yet implemented");
        return 0;
    }

    private static int RunUsage()
    {
        Console.Error.WriteLine("Usage: --config <subcommand>");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Subcommands:");
        Console.Error.WriteLine("  show [--reveal]       Display all config values with source annotations");
        Console.Error.WriteLine("  set <key>=<value>     Set a config value (add --project for project scope)");
        Console.Error.WriteLine("  get <key>             Get the resolved value for a key");
        Console.Error.WriteLine("  init                  Create starter config with local preset");
        Console.Error.WriteLine("  reset [<key>]         Reset a key or all config values");
        Console.Error.WriteLine("  use <profile>         Switch active profile");
        Console.Error.WriteLine("  env                   Generate shell env var template");
        Console.Error.WriteLine("  validate              Test Qdrant connection and embedding provider");
        Console.Error.WriteLine();
        Console.Error.WriteLine("No subcommand enters interactive wizard.");
        return 1;
    }
}
