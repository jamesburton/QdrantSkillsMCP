using System.Reflection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace QdrantSkillsMCP.Infrastructure.Setup;

/// <summary>
/// Orchestrates the --setup wizard: detects installed agents, writes MCP config,
/// and places SKILL.md in agent skill directories. Supports both interactive and
/// non-interactive (--agent + --level flags) modes.
/// </summary>
public sealed class SetupWizard
{
    private readonly AgentDetector _detector;
    private readonly IReadOnlyList<IAgentConfigWriter> _writers;
    private readonly ILogger<SetupWizard> _logger;

    public SetupWizard(
        AgentDetector detector,
        IEnumerable<IAgentConfigWriter> writers,
        ILogger<SetupWizard> logger)
    {
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _writers = writers?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(writers));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Entry point for setup wizard. Parses args and routes to interactive or non-interactive flow.
    /// </summary>
    public async Task<int> RunAsync(string[] args)
    {
        var (agentName, level) = ParseArgs(args);

        // Both provided: non-interactive
        if (agentName is not null && level is not null)
        {
            return await RunNonInteractiveAsync(agentName, level.Value);
        }

        // Neither provided: interactive
        if (agentName is null && level is null)
        {
            return await RunInteractiveAsync();
        }

        // Partial: error
        Console.Error.WriteLine("Usage: --setup [--agent <name> --level <user|project>]");
        Console.Error.WriteLine("  Provide both --agent and --level for non-interactive mode,");
        Console.Error.WriteLine("  or neither for interactive mode.");
        return 1;
    }

    /// <summary>
    /// Parse --agent and --level from args. Exposed for testability.
    /// </summary>
    internal static (string? AgentName, AgentScope? Level) ParseArgs(string[] args)
    {
        string? agentName = null;
        AgentScope? level = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--agent", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                agentName = args[++i];
            }
            else if (args[i].Equals("--level", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var val = args[++i];
                if (Enum.TryParse<AgentScope>(val, ignoreCase: true, out var scope))
                    level = scope;
                else
                {
                    Console.Error.WriteLine($"Unknown level: {val}. Expected 'user' or 'project'.");
                    return (agentName, null);
                }
            }
        }

        return (agentName, level);
    }

    /// <summary>
    /// Find a writer by agent name or writer ID (case-insensitive).
    /// Exposed for testability.
    /// </summary>
    internal IAgentConfigWriter? FindWriter(string agentName)
    {
        return _writers.FirstOrDefault(w =>
            w.WriterId.Equals(agentName, StringComparison.OrdinalIgnoreCase) ||
            w.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<int> RunNonInteractiveAsync(string agentName, AgentScope scope)
    {
        var writer = FindWriter(agentName);
        if (writer is null)
        {
            Console.Error.WriteLine($"Unknown agent: {agentName}");
            Console.Error.WriteLine($"Available agents: {string.Join(", ", _writers.Select(w => w.WriterId))}");
            return 1;
        }

        if (!writer.SupportedScopes.Contains(scope))
        {
            Console.Error.WriteLine($"Agent '{writer.AgentName}' does not support {scope} scope.");
            Console.Error.WriteLine($"Supported scopes: {string.Join(", ", writer.SupportedScopes)}");
            return 1;
        }

        var entry = CreateDefaultEntry();

        if (writer.CanAutoWrite)
        {
            var configPath = writer.DetectInstallation(scope)
                ?? GetDefaultConfigPath(writer, scope);

            if (configPath is null)
            {
                Console.Error.WriteLine($"Could not determine config path for {writer.AgentName} at {scope} scope.");
                return 1;
            }

            await writer.WriteConfigAsync(configPath, entry);
            Console.WriteLine($"Wrote MCP config for {writer.AgentName} to {configPath}");

            await WriteSkillFileAsync(writer);
        }
        else
        {
            var snippet = writer.GenerateSnippet(entry, scope);
            Console.WriteLine(snippet);
        }

        return 0;
    }

    private async Task<int> RunInteractiveAsync()
    {
        var detected = _detector.DetectInstalledAgents();

        Console.WriteLine("QdrantSkillsMCP Setup Wizard");
        Console.WriteLine("===========================\n");

        if (detected.Count > 0)
        {
            Console.WriteLine($"Detected {detected.Count} installed agent(s):\n");
            foreach (var agent in detected)
            {
                Console.WriteLine($"  - {agent.Name} ({agent.Scope}) at {agent.ConfigPath}");
            }
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("No agents detected. Showing all available agents.\n");
        }

        // Build agent selection list
        var choices = _writers
            .Where(w => w.CanAutoWrite)
            .Select(w => w.AgentName)
            .ToList();

        if (choices.Count == 0)
        {
            Console.Error.WriteLine("No auto-write capable agents available.");
            return 1;
        }

        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Select agents to configure:")
                .AddChoices(choices)
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]"));

        if (selected.Count == 0)
        {
            Console.WriteLine("No agents selected. Exiting.");
            return 0;
        }

        var entry = CreateDefaultEntry();
        var configuredWriters = new HashSet<string>();

        foreach (var agentName in selected)
        {
            var writer = _writers.First(w => w.AgentName == agentName);

            AgentScope scope;
            if (writer.SupportedScopes.Length == 1)
            {
                scope = writer.SupportedScopes[0];
            }
            else
            {
                scope = AnsiConsole.Prompt(
                    new SelectionPrompt<AgentScope>()
                        .Title($"Select scope for {writer.AgentName}:")
                        .AddChoices(writer.SupportedScopes));
            }

            var configPath = writer.DetectInstallation(scope)
                ?? GetDefaultConfigPath(writer, scope);

            if (configPath is not null)
            {
                await writer.WriteConfigAsync(configPath, entry);
                Console.WriteLine($"  Configured {writer.AgentName} ({scope}) at {configPath}");
                configuredWriters.Add(writer.WriterId);
            }
        }

        // Write SKILL.md for agents that support it (deduplicate)
        foreach (var writerId in configuredWriters)
        {
            var writer = _writers.First(w => w.WriterId == writerId);
            await WriteSkillFileAsync(writer);
        }

        Console.WriteLine($"\nSetup complete. Configured {configuredWriters.Count} agent(s).");
        return 0;
    }

    /// <summary>
    /// Reads SKILL.md from embedded resource and writes to the agent's skill directory.
    /// Does nothing if the agent has no skill directory or the resource is not embedded.
    /// </summary>
    internal async Task WriteSkillFileAsync(IAgentConfigWriter writer)
    {
        if (writer.SkillDirectoryPath is null)
        {
            _logger.LogDebug("Agent {Agent} has no skill directory, skipping SKILL.md placement", writer.AgentName);
            return;
        }

        var assembly = typeof(SetupWizard).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "QdrantSkillsMCP.Infrastructure.SkillGuide.SKILL.md");

        if (stream is null)
        {
            _logger.LogWarning(
                "SKILL.md embedded resource not found in assembly. " +
                "This is expected if Plan 02 (SKILL.md bundling) has not run yet.");
            return;
        }

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        Directory.CreateDirectory(writer.SkillDirectoryPath);
        var targetPath = Path.Combine(writer.SkillDirectoryPath, "SKILL.md");
        await File.WriteAllTextAsync(targetPath, content);

        _logger.LogInformation("Wrote SKILL.md to {Path}", targetPath);
        Console.WriteLine($"  Placed SKILL.md at {targetPath}");
    }

    private static McpServerEntry CreateDefaultEntry() =>
        new("qdrant-skills-mcp", "dnx", ["qdrant-skills-mcp"]);

    private static string? GetDefaultConfigPath(IAgentConfigWriter writer, AgentScope scope)
    {
        // Use DetectInstallation which constructs default paths
        return writer.DetectInstallation(scope);
    }
}
