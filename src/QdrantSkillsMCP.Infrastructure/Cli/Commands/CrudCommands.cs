using Microsoft.Extensions.DependencyInjection;
using QdrantSkillsMCP.Core.Interfaces;
using QdrantSkillsMCP.Infrastructure.Yaml;

namespace QdrantSkillsMCP.Infrastructure.Cli.Commands;

/// <summary>
/// CLI commands for add, update, delete, and archive operations.
/// </summary>
internal static class CrudCommands
{
    public static async Task<int> AddAsync(
        IServiceProvider services, string[] args, CancellationToken ct)
    {
        var content = await ReadSkillContentAsync(args);
        if (content is null)
        {
            Console.Error.WriteLine("Usage: add --file <path> or pipe content via stdin");
            return 1;
        }

        var parser = services.GetRequiredService<SkillParser>();
        var repo = services.GetRequiredService<ISkillRepository>();
        var embedding = services.GetRequiredService<IEmbeddingService>();

        var skill = parser.ToSkill(content);
        var embeddingText = string.IsNullOrEmpty(skill.Description)
            ? skill.MarkdownBody
            : $"{skill.Description}\n\n{skill.MarkdownBody}";
        var vector = await embedding.GenerateEmbeddingAsync(embeddingText, ct);
        await repo.AddAsync(skill, vector, overwrite: false, ct);

        Console.WriteLine($"Added skill: {skill.Name}");
        return 0;
    }

    public static async Task<int> UpdateAsync(
        IServiceProvider services, string[] args, CancellationToken ct)
    {
        var content = await ReadSkillContentAsync(args);
        if (content is null)
        {
            Console.Error.WriteLine("Usage: update --file <path> or pipe content via stdin");
            return 1;
        }

        var parser = services.GetRequiredService<SkillParser>();
        var repo = services.GetRequiredService<ISkillRepository>();
        var embedding = services.GetRequiredService<IEmbeddingService>();

        var skill = parser.ToSkill(content);
        var embeddingText = string.IsNullOrEmpty(skill.Description)
            ? skill.MarkdownBody
            : $"{skill.Description}\n\n{skill.MarkdownBody}";
        var vector = await embedding.GenerateEmbeddingAsync(embeddingText, ct);
        await repo.UpdateAsync(skill, vector, ct);

        Console.WriteLine($"Updated skill: {skill.Name}");
        return 0;
    }

    public static async Task<int> DeleteAsync(
        IServiceProvider services, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: delete <skill-name>");
            return 1;
        }

        var repo = services.GetRequiredService<ISkillRepository>();
        await repo.DeleteAsync(args[0], ct);

        Console.WriteLine($"Deleted skill: {args[0]}");
        return 0;
    }

    public static async Task<int> ArchiveAsync(
        IServiceProvider services, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: archive <skill-name>");
            return 1;
        }

        var repo = services.GetRequiredService<ISkillRepository>();
        await repo.ArchiveAsync(args[0], ct);

        Console.WriteLine($"Archived skill: {args[0]}");
        return 0;
    }

    private static async Task<string?> ReadSkillContentAsync(string[] args)
    {
        // Check for --file flag
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--file" && i + 1 < args.Length)
            {
                var path = args[i + 1];
                if (!File.Exists(path))
                {
                    Console.Error.WriteLine($"File not found: {path}");
                    return null;
                }
                return await File.ReadAllTextAsync(path);
            }
        }

        // Try reading from stdin if redirected
        if (Console.IsInputRedirected)
        {
            return await Console.In.ReadToEndAsync();
        }

        return null;
    }
}
