using Microsoft.Extensions.DependencyInjection;
using QdrantSkillsMCP.Core.Interfaces;

namespace QdrantSkillsMCP.Infrastructure.Cli.Commands;

/// <summary>
/// CLI command: search &lt;query&gt; [--max N] [--temp 0.X]
/// </summary>
internal static class SearchCommand
{
    public static async Task<int> RunAsync(
        IServiceProvider services, string[] args, ConsoleOutputFormatter formatter, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: search <query> [--max N] [--temp 0.X]");
            return 1;
        }

        var repo = services.GetRequiredService<ISkillRepository>();
        var embedding = services.GetRequiredService<IEmbeddingService>();

        // Parse args: first non-flag tokens are the query, --max and --temp are options
        var queryParts = new List<string>();
        int maxResults = 10;
        float? scoreThreshold = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--max" && i + 1 < args.Length && int.TryParse(args[i + 1], out var max))
            {
                maxResults = max;
                i++;
            }
            else if (args[i] == "--temp" && i + 1 < args.Length && float.TryParse(args[i + 1], out var temp))
            {
                scoreThreshold = 1.0f - temp;
                i++;
            }
            else
            {
                queryParts.Add(args[i]);
            }
        }

        var query = string.Join(' ', queryParts);
        var vector = await embedding.GenerateEmbeddingAsync(query, ct);
        var results = await repo.SearchAsync(vector, maxResults, scoreThreshold, ct);

        formatter.FormatSearchResults(results);
        return 0;
    }
}
