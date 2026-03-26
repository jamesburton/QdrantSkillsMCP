using System.Text.Json;
using System.Text.Json.Serialization;
using QdrantSkillsMCP.Core.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace QdrantSkillsMCP.Infrastructure.Cli;

/// <summary>
/// Formats CLI output as human-readable Spectre.Console tables or JSON.
/// Uses a custom IAnsiConsole that writes to Console.Out so that Console.SetOut
/// redirects work correctly (important for testing).
/// </summary>
public sealed class ConsoleOutputFormatter
{
    private readonly bool _jsonOutput;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ConsoleOutputFormatter(bool jsonOutput)
    {
        _jsonOutput = jsonOutput;
    }

    /// <summary>Formats search results as a table or JSON array.</summary>
    public void FormatSearchResults(IReadOnlyList<SearchResult> results)
    {
        if (_jsonOutput)
        {
            var dtos = results.Select(r => new
            {
                r.Skill.Name,
                r.Score,
                r.Skill.Description,
                r.Skill.Tags
            });
            Console.WriteLine(JsonSerializer.Serialize(dtos, JsonOptions));
            return;
        }

        if (results.Count == 0)
        {
            Console.WriteLine("No results found.");
            return;
        }

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Score");
        table.AddColumn("Description");

        foreach (var r in results)
        {
            table.AddRow(
                Markup.Escape(r.Skill.Name),
                r.Score.ToString("F3"),
                Markup.Escape(r.Skill.Description ?? ""));
        }

        WriteRenderable(table);
    }

    /// <summary>Formats skill metadata list as a table or JSON array.</summary>
    public void FormatSkillList(IReadOnlyList<SkillMetadata> skills)
    {
        if (_jsonOutput)
        {
            var dtos = skills.Select(s => new
            {
                s.Name,
                s.Description,
                s.Tags,
                s.UpdatedAt
            });
            Console.WriteLine(JsonSerializer.Serialize(dtos, JsonOptions));
            return;
        }

        if (skills.Count == 0)
        {
            Console.WriteLine("No skills found.");
            return;
        }

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Description");
        table.AddColumn("Tags");

        foreach (var s in skills)
        {
            table.AddRow(
                Markup.Escape(s.Name),
                Markup.Escape(s.Description ?? ""),
                Markup.Escape(s.Tags != null ? string.Join(", ", s.Tags) : ""));
        }

        WriteRenderable(table);
    }

    /// <summary>Formats a full skill for display.</summary>
    public void FormatSkill(Skill skill)
    {
        if (_jsonOutput)
        {
            var dto = new
            {
                skill.Name,
                skill.Description,
                skill.Tags,
                skill.RawContent,
                skill.UpdatedAt,
                skill.Archived
            };
            Console.WriteLine(JsonSerializer.Serialize(dto, JsonOptions));
            return;
        }

        Console.WriteLine($"=== {skill.Name} ===");
        if (skill.Description != null)
            Console.WriteLine($"Description: {skill.Description}");
        if (skill.Tags is { Length: > 0 })
            Console.WriteLine($"Tags: {string.Join(", ", skill.Tags)}");
        Console.WriteLine();
        Console.WriteLine(skill.MarkdownBody);
    }

    /// <summary>Formats connection/status information.</summary>
    public void FormatStatus(string host, int port, string collection, string? provider, int dimensions)
    {
        if (_jsonOutput)
        {
            var dto = new { host, port, collection, provider, dimensions };
            Console.WriteLine(JsonSerializer.Serialize(dto, JsonOptions));
            return;
        }

        var table = new Table();
        table.AddColumn("Setting");
        table.AddColumn("Value");

        table.AddRow("Qdrant Host", Markup.Escape(host));
        table.AddRow("Qdrant Port", port.ToString());
        table.AddRow("Collection", Markup.Escape(collection));
        table.AddRow("Embedding Provider", Markup.Escape(provider ?? "not configured"));
        table.AddRow("Vector Dimensions", dimensions.ToString());

        WriteRenderable(table);
    }

    /// <summary>
    /// Writes a Spectre.Console renderable to the current Console.Out.
    /// Creates a fresh IAnsiConsole each time to respect Console.SetOut redirects.
    /// </summary>
    private static void WriteRenderable(IRenderable renderable)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(Console.Out)
        });
        console.Write(renderable);
    }
}
