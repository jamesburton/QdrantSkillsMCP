using QdrantSkillsMCP.Infrastructure.Cli.Commands;

namespace QdrantSkillsMCP.Infrastructure.Cli;

/// <summary>
/// CLI entry point: parses args, dispatches to subcommands or REPL.
/// </summary>
public sealed class ConsoleHost
{
    private readonly IServiceProvider _services;

    public ConsoleHost(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>
    /// Runs the CLI with the given args. Returns process exit code.
    /// </summary>
    public async Task<int> RunAsync(string[] args, CancellationToken ct = default)
    {
        // Strip --console and --json flags, determine output mode
        var remaining = new List<string>();
        bool jsonOutput = false;

        foreach (var arg in args)
        {
            if (arg == "--console") continue;
            if (arg == "--json") { jsonOutput = true; continue; }
            remaining.Add(arg);
        }

        var formatter = new ConsoleOutputFormatter(jsonOutput);

        // No subcommand -> REPL
        if (remaining.Count == 0)
        {
            var repl = new ReplLoop(_services, formatter);
            return await repl.RunAsync(ct);
        }

        var command = remaining[0].ToLowerInvariant();
        var commandArgs = remaining.Skip(1).ToArray();

        return command switch
        {
            "search" => await SearchCommand.RunAsync(_services, commandArgs, formatter, ct),
            "list" => await ListCommand.RunAsync(_services, formatter, ct),
            "load" => await LoadCommand.RunAsync(_services, commandArgs, formatter, ct),
            "add" => await CrudCommands.AddAsync(_services, commandArgs, ct),
            "update" => await CrudCommands.UpdateAsync(_services, commandArgs, ct),
            "delete" => await CrudCommands.DeleteAsync(_services, commandArgs, ct),
            "archive" => await CrudCommands.ArchiveAsync(_services, commandArgs, ct),
            "status" or "info" => await StatusCommand.RunAsync(_services, formatter, ct),
            _ => UnknownCommand(command)
        };
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine("Available commands: search, list, load, add, update, delete, archive, status, help, exit");
        return 1;
    }
}
