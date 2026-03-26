using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using QdrantSkillsMCP.Infrastructure;
using QdrantSkillsMCP.Infrastructure.Cli;

// Mode branching: --console, --setup, or default MCP server
if (args.Contains("--console"))
{
    // CLI mode: stdout is safe for output (no MCP transport)
    var builder = Host.CreateApplicationBuilder(args);
    builder.Configuration.AddJsonFile("qdrant-skills.json", optional: true, reloadOnChange: false);
    builder.Services.AddQdrantSkillsInfrastructure(builder.Configuration);

    var host = builder.Build();
    var consoleHost = new ConsoleHost(host.Services);
    var exitCode = await consoleHost.RunAsync(args);
    Environment.Exit(exitCode);
}
else if (args.Contains("--setup"))
{
    // Setup wizard mode: placeholder for Plan 02
    // Does NOT register infrastructure services (setup doesn't need Qdrant connection)
    var builder = Host.CreateApplicationBuilder(args);
    Console.WriteLine("Setup wizard not yet implemented. Coming in a future update.");
    Environment.Exit(0);
}
else
{
    // Default: MCP server mode (existing behavior)
    var builder = Host.CreateApplicationBuilder(args);

    // CRITICAL: ALL logging to stderr. Stdout is reserved for MCP JSON-RPC transport.
    builder.Logging.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });

    builder.Configuration.AddJsonFile("qdrant-skills.json", optional: true, reloadOnChange: false);
    builder.Services.AddQdrantSkillsInfrastructure(builder.Configuration);

    // MCP server with stdio transport -- tools auto-discovered from this assembly
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    await builder.Build().RunAsync();
}
