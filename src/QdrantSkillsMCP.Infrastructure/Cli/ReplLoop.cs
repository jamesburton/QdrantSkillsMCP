using Microsoft.Extensions.DependencyInjection;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Infrastructure.Cli.Commands;

namespace QdrantSkillsMCP.Infrastructure.Cli;

/// <summary>
/// Interactive REPL with tab completion for skill names and command history.
/// </summary>
public sealed class ReplLoop
{
    private readonly IServiceProvider _services;
    private readonly ConsoleOutputFormatter _formatter;
    private readonly List<string> _history = new();
    private List<string> _skillNames = new();

    public ReplLoop(IServiceProvider services, ConsoleOutputFormatter formatter)
    {
        _services = services;
        _formatter = formatter;
    }

    /// <summary>
    /// Runs the interactive REPL loop. Returns process exit code.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        // Pre-fetch skill names for tab completion
        await LoadSkillNamesAsync(ct);

        Console.WriteLine("QdrantSkillsMCP REPL. Type 'help' for commands, 'exit' to quit.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        while (!cts.IsCancellationRequested)
        {
            Console.Write("> ");

            string? line;
            try
            {
                line = ReadLineWithCompletion(cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine();
                break;
            }

            if (line is null)
            {
                Console.WriteLine();
                break;
            }

            var result = await ProcessCommandAsync(line, cts.Token);
            if (!result.Continue)
                return result.ExitCode;
        }

        return 0;
    }

    /// <summary>
    /// Processes a single command line. Exposed for unit testing.
    /// </summary>
    public async Task<CommandResult> ProcessCommandAsync(string line, CancellationToken ct = default)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return CommandResult.ContinueResult;

        // Add to history
        if (_history.Count == 0 || _history[^1] != trimmed)
            _history.Add(trimmed);

        // Parse command and args
        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();
        var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

        switch (command)
        {
            case "exit":
            case "quit":
                return new CommandResult(Continue: false, ExitCode: 0);

            case "help":
                PrintHelp();
                return CommandResult.ContinueResult;

            case "search":
                await SearchCommand.RunAsync(_services, args, _formatter, ct);
                return CommandResult.ContinueResult;

            case "list":
                await ListCommand.RunAsync(_services, _formatter, ct);
                return CommandResult.ContinueResult;

            case "load":
                await LoadCommand.RunAsync(_services, args, _formatter, ct);
                return CommandResult.ContinueResult;

            case "add":
                await CrudCommands.AddAsync(_services, args, ct);
                return CommandResult.ContinueResult;

            case "update":
                await CrudCommands.UpdateAsync(_services, args, ct);
                return CommandResult.ContinueResult;

            case "delete":
                await CrudCommands.DeleteAsync(_services, args, ct);
                return CommandResult.ContinueResult;

            case "archive":
                await CrudCommands.ArchiveAsync(_services, args, ct);
                return CommandResult.ContinueResult;

            case "status":
            case "info":
                await StatusCommand.RunAsync(_services, _formatter, ct);
                return CommandResult.ContinueResult;

            default:
                Console.Error.WriteLine($"Unknown command: {command}. Type 'help' for available commands.");
                return CommandResult.ContinueResult;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Available commands:");
        Console.WriteLine("  search <query>  Search skills semantically");
        Console.WriteLine("  list            List all skills");
        Console.WriteLine("  load <name>     Load a skill by name");
        Console.WriteLine("  add             Add a skill (--file <path> or stdin)");
        Console.WriteLine("  update          Update a skill (--file <path> or stdin)");
        Console.WriteLine("  delete <name>   Delete a skill");
        Console.WriteLine("  archive <name>  Archive a skill");
        Console.WriteLine("  status          Show connection info");
        Console.WriteLine("  help            Show this help");
        Console.WriteLine("  exit            Exit the REPL");
    }

    private async Task LoadSkillNamesAsync(CancellationToken ct)
    {
        try
        {
            var repo = _services.GetRequiredService<ISkillRepository>();
            var skills = await repo.ListAsync(ct);
            _skillNames = skills.Select(s => s.Name).OrderBy(n => n).ToList();
        }
        catch
        {
            // Non-fatal: tab completion just won't work if this fails
            _skillNames = new List<string>();
        }
    }

    /// <summary>
    /// Reads a line with tab completion and history navigation.
    /// </summary>
    private string? ReadLineWithCompletion(CancellationToken ct)
    {
        // If input is redirected (piping), just use ReadLine
        if (Console.IsInputRedirected)
            return Console.ReadLine();

        var buffer = new List<char>();
        int cursorPos = 0;
        int historyIndex = _history.Count;
        int tabIndex = -1;
        string tabPrefix = "";
        List<string>? tabCandidates = null;

        while (!ct.IsCancellationRequested)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    var result = new string(buffer.ToArray());
                    return result;

                case ConsoleKey.Backspace:
                    if (cursorPos > 0)
                    {
                        buffer.RemoveAt(cursorPos - 1);
                        cursorPos--;
                        RedrawLine(buffer, cursorPos);
                    }
                    tabCandidates = null;
                    break;

                case ConsoleKey.Delete:
                    if (cursorPos < buffer.Count)
                    {
                        buffer.RemoveAt(cursorPos);
                        RedrawLine(buffer, cursorPos);
                    }
                    tabCandidates = null;
                    break;

                case ConsoleKey.LeftArrow:
                    if (cursorPos > 0)
                    {
                        cursorPos--;
                        Console.CursorLeft--;
                    }
                    break;

                case ConsoleKey.RightArrow:
                    if (cursorPos < buffer.Count)
                    {
                        cursorPos++;
                        Console.CursorLeft++;
                    }
                    break;

                case ConsoleKey.Home:
                    Console.CursorLeft -= cursorPos;
                    cursorPos = 0;
                    break;

                case ConsoleKey.End:
                    Console.CursorLeft += buffer.Count - cursorPos;
                    cursorPos = buffer.Count;
                    break;

                case ConsoleKey.UpArrow:
                    if (historyIndex > 0)
                    {
                        historyIndex--;
                        ReplaceBuffer(buffer, _history[historyIndex], ref cursorPos);
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if (historyIndex < _history.Count - 1)
                    {
                        historyIndex++;
                        ReplaceBuffer(buffer, _history[historyIndex], ref cursorPos);
                    }
                    else if (historyIndex == _history.Count - 1)
                    {
                        historyIndex = _history.Count;
                        ReplaceBuffer(buffer, "", ref cursorPos);
                    }
                    break;

                case ConsoleKey.Tab:
                    HandleTabCompletion(buffer, ref cursorPos, ref tabIndex, ref tabPrefix, ref tabCandidates);
                    break;

                default:
                    if (key.KeyChar >= 32) // printable character
                    {
                        buffer.Insert(cursorPos, key.KeyChar);
                        cursorPos++;
                        RedrawLine(buffer, cursorPos);
                        tabCandidates = null;
                    }
                    break;
            }
        }

        ct.ThrowIfCancellationRequested();
        return null;
    }

    private void HandleTabCompletion(
        List<char> buffer, ref int cursorPos,
        ref int tabIndex, ref string tabPrefix, ref List<string>? tabCandidates)
    {
        var currentLine = new string(buffer.ToArray());
        var parts = currentLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Tab complete skill names after "load", "search", "delete", "archive"
        if (parts.Length >= 1 && tabCandidates is null)
        {
            var cmd = parts[0].ToLowerInvariant();
            if (cmd is "load" or "search" or "delete" or "archive")
            {
                tabPrefix = parts.Length > 1 ? parts[^1] : "";
                var prefix = tabPrefix;
                tabCandidates = _skillNames
                    .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                tabIndex = -1;
            }
            else if (parts.Length == 1 && !currentLine.EndsWith(' '))
            {
                // Tab complete command names
                var commands = new[] { "search", "list", "load", "add", "update", "delete", "archive", "status", "help", "exit" };
                tabPrefix = parts[0];
                var prefix = tabPrefix;
                tabCandidates = commands
                    .Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                tabIndex = -1;
            }
        }

        if (tabCandidates is { Count: > 0 })
        {
            tabIndex = (tabIndex + 1) % tabCandidates.Count;
            var completion = tabCandidates[tabIndex];

            // Replace the current token with the completion
            var lastSpace = currentLine.LastIndexOf(' ');
            var newLine = lastSpace >= 0
                ? currentLine[..(lastSpace + 1)] + completion
                : completion;

            ReplaceBuffer(buffer, newLine, ref cursorPos);
        }
    }

    private static void ReplaceBuffer(List<char> buffer, string newContent, ref int cursorPos)
    {
        // Clear current line display
        var promptLen = 2; // "> "
        try
        {
            Console.CursorLeft = promptLen;
            Console.Write(new string(' ', buffer.Count));
            Console.CursorLeft = promptLen;
        }
        catch (IOException)
        {
            // Ignore if console doesn't support cursor manipulation
        }

        buffer.Clear();
        buffer.AddRange(newContent);
        cursorPos = buffer.Count;

        Console.Write(newContent);
    }

    private static void RedrawLine(List<char> buffer, int cursorPos)
    {
        var promptLen = 2; // "> "
        try
        {
            Console.CursorLeft = promptLen;
            var text = new string(buffer.ToArray());
            Console.Write(text + " "); // extra space to clear trailing char on backspace
            Console.CursorLeft = promptLen + cursorPos;
        }
        catch (IOException)
        {
            // Ignore if console doesn't support cursor manipulation
        }
    }

    /// <summary>Result of processing a single REPL command.</summary>
    public record CommandResult(bool Continue, int ExitCode)
    {
        public static readonly CommandResult ContinueResult = new(Continue: true, ExitCode: 0);
    }
}
