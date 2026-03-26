using Microsoft.Extensions.DependencyInjection;
using QdrantSkillsMCP.Core.Interfaces;

namespace QdrantSkillsMCP.Infrastructure.Cli.Commands;

/// <summary>
/// CLI command: list
/// </summary>
internal static class ListCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services, ConsoleOutputFormatter formatter, CancellationToken ct)
    {
        var repo = services.GetRequiredService<ISkillRepository>();
        var skills = await repo.ListAsync(ct);
        formatter.FormatSkillList(skills);
        return 0;
    }
}
