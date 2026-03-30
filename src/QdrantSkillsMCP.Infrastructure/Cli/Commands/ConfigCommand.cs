using Qdrant.Client;
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

        try
        {
            await configManager.SetValueAsync(key, value, projectScope);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        var displayValue = SecretMask.IsSecret(key) ? SecretMask.Mask(value) : value;
        Console.WriteLine($"Set {key} = {displayValue}{(projectScope ? " [project]" : " [user]")}");
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
    /// Validates Qdrant connectivity and embedding provider configuration.
    /// </summary>
    internal static async Task<int> RunValidate(ConfigManager configManager)
    {
        var entries = configManager.GetAllWithSources();
        var host = entries.GetValueOrDefault("QdrantHost")?.Value ?? "localhost";
        var portStr = entries.GetValueOrDefault("QdrantGrpcPort")?.Value ?? "6334";
        var collection = entries.GetValueOrDefault("CollectionName")?.Value ?? "skills";
        var apiKey = entries.GetValueOrDefault("QdrantApiKey")?.Value;
        var useTlsStr = entries.GetValueOrDefault("UseTls")?.Value;
        var useTls = string.Equals(useTlsStr, "true", StringComparison.OrdinalIgnoreCase)
                  || string.Equals(useTlsStr, "True", StringComparison.OrdinalIgnoreCase);

        if (!int.TryParse(portStr, out var port))
            port = 6334;

        var isLocalhost = host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host == "127.0.0.1"
            || host == "::1";

        // Auto-enable TLS for remote hosts unless explicitly set
        if (!isLocalhost && !useTls && useTlsStr is null)
        {
            useTls = true;
            Console.WriteLine("INFO: Remote host detected — auto-enabling TLS. Set UseTls=false to override.");
        }

        Console.WriteLine($"Resolved config: host={host}, port={port}, collection={collection}, tls={useTls}");

        var allPassed = true;

        // Test Qdrant connection
        Console.Write("Qdrant connection... ");
        try
        {
            var client = new QdrantClient(host, port, https: useTls, apiKey: apiKey);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await client.ListCollectionsAsync(cts.Token);
            Console.WriteLine("PASS");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL ({ex.GetType().Name}: {ex.Message})");

            // Suggest TLS toggle if connection failed
            if (!useTls && !isLocalhost)
                Console.WriteLine("HINT: Try setting UseTls=true for remote hosts (--config set UseTls=true)");
            else if (useTls && ex.Message.Contains("HTTP_1_1_REQUIRED", StringComparison.OrdinalIgnoreCase))
                Console.WriteLine("HINT: Host requires HTTP/1.1 — gRPC over HTTP/2 may not be supported by this proxy. Try the Qdrant REST port instead.");

            allPassed = false;
        }

        return allPassed ? 0 : 1;
    }

    /// <summary>
    /// Interactive wizard loop. Presents a menu and routes to subcommand operations.
    /// </summary>
    internal static async Task<int> RunInteractiveAsync(ConfigManager configManager)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(Console.Out)
        });

        Console.WriteLine("QdrantSkills Configuration Wizard");
        Console.WriteLine("=================================\n");

        while (true)
        {
            var choice = console.Prompt(
                new SelectionPrompt<string>()
                    .Title("What would you like to do?")
                    .AddChoices(
                        "Show config",
                        "Set a value",
                        "Initialize config",
                        "Switch profile",
                        "Generate env vars",
                        "Validate connection",
                        "Reset",
                        "Exit"));

            switch (choice)
            {
                case "Show config":
                    RunShow(configManager, ["--config", "show"]);
                    break;
                case "Set a value":
                    var key = console.Prompt(new SelectionPrompt<string>()
                        .Title("Select key to set:")
                        .AddChoices(ConfigManager.ConfigurableKeys));
                    var currentValue = configManager.GetValue(key);
                    var example = GetExample(key);
                    var prompt = new TextPrompt<string>($"Enter value for [green]{key}[/]{example}:");
                    if (currentValue is not null)
                        prompt.DefaultValue(currentValue);
                    prompt.AllowEmpty();
                    var value = console.Prompt(prompt);
                    if (string.IsNullOrEmpty(value))
                    {
                        Console.WriteLine("(skipped)");
                        break;
                    }
                    await configManager.SetValueAsync(key, value);
                    Console.WriteLine($"Set {key} = {value}");
                    break;
                case "Initialize config":
                    await RunInit(configManager);
                    break;
                case "Switch profile":
                    var profiles = configManager.GetProfiles();
                    var profileName = profiles.Count > 0
                        ? console.Prompt(new TextPrompt<string>("Profile name:")
                            .DefaultValue(profiles[0]))
                        : console.Prompt(new TextPrompt<string>("Profile name:"));
                    await configManager.UseProfileAsync(profileName);
                    Console.WriteLine($"Switched to profile: {profileName}");
                    break;
                case "Generate env vars":
                    RunEnv(configManager);
                    break;
                case "Validate connection":
                    await RunValidate(configManager);
                    break;
                case "Reset":
                    await RunReset(configManager, ["--config", "reset"]);
                    break;
                case "Exit":
                    return 0;
            }

            Console.WriteLine();
        }
    }

    private static string GetExample(string key) => key switch
    {
        "QdrantHost" => " (e.g. localhost, qdrant-qhub.azurewebsites.net)",
        "QdrantGrpcPort" => " (e.g. 6334 for local, 443 or 80 for cloud)",
        "QdrantApiKey" => " (your Qdrant API key)",
        "CollectionName" => " (e.g. skills)",
        "UseTls" => " (true for remote/HTTPS hosts, false for local)",
        "EmbeddingProvider" => " (OpenAI, LocalONNX, Ollama, AzureOpenAI)",
        "OpenAiApiKey" => " (sk-...)",
        "EmbeddingModel" => " (e.g. text-embedding-3-small)",
        "EmbeddingUrl" => " (e.g. http://localhost:11434 for Ollama)",
        "AzureOpenAiEndpoint" => " (e.g. https://myresource.openai.azure.com/)",
        "AzureOpenAiApiKey" => " (your Azure OpenAI key)",
        "AzureOpenAiDeployment" => " (e.g. text-embedding-ada-002)",
        _ => ""
    };

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
