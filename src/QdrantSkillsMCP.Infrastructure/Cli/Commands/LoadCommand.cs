using Microsoft.Extensions.DependencyInjection;
using QdrantSkillsMCP.Core.Interfaces;

namespace QdrantSkillsMCP.Infrastructure.Cli.Commands;

/// <summary>
/// CLI command: load &lt;name&gt; [name2 ...]
/// </summary>
internal static class LoadCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services, string[] args, ConsoleOutputFormatter formatter, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: load <skill-name> [skill-name2 ...]");
            return 1;
        }

        var repo = services.GetRequiredService<ISkillRepository>();

        foreach (var name in args)
        {
            var skill = await repo.GetByNameAsync(name, ct);
            if (skill is null)
            {
                Console.Error.WriteLine($"Skill not found: {name}");
                continue;
            }

            formatter.FormatSkill(skill);

            if (args.Length > 1)
                Console.WriteLine(); // separator between multiple skills
        }

        return 0;
    }
}
