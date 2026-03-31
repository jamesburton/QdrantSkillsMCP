using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using QdrantSkillsMCP.Infrastructure;
using QdrantSkillsMCP.Infrastructure.Cli;
using QdrantSkillsMCP.Infrastructure.Cli.Commands;
using QdrantSkillsMCP.Infrastructure.Configuration;
using QdrantSkillsMCP.Infrastructure.Setup;

// Mode branching: --config, --console, --setup, or default MCP server
if (args.Contains("--config"))
{
    // Config command mode: lightweight, no Qdrant DI needed
    var configManager = new ConfigManager();
    var exitCode = await ConfigCommand.RunAsync(configManager, args);
    Environment.Exit(exitCode);
}
else if (args.Contains("--console"))
{
    // CLI mode: stdout is safe for output (no MCP transport)
    var builder = Host.CreateApplicationBuilder(args);
    builder.Logging.ClearProviders().AddConsole();
    UserConfigLoader.AddUserConfig(builder.Configuration);
    builder.Configuration.AddJsonFile("qdrant-skills.json", optional: true, reloadOnChange: false);
    ApplyQdrantProtocolFlags(builder, args);
    builder.Services.AddQdrantSkillsInfrastructure(builder.Configuration);

    var host = builder.Build();
    var consoleHost = new ConsoleHost(host.Services);
    var exitCode = await consoleHost.RunAsync(args);
    Environment.Exit(exitCode);
}
else if (args.Contains("--setup"))
{
    // Setup wizard mode: registers only setup services (no Qdrant connection needed)
    var builder = Host.CreateApplicationBuilder(args);
    builder.Logging.ClearProviders().AddConsole();
    UserConfigLoader.AddUserConfig(builder.Configuration);
    builder.Services.AddSetupServices();

    var host = builder.Build();
    var wizard = host.Services.GetRequiredService<SetupWizard>();
    var exitCode = await wizard.RunAsync(args);
    Environment.Exit(exitCode);
}
else
{
    // Default: MCP server mode (existing behavior)
    var builder = Host.CreateApplicationBuilder(args);

    // CRITICAL: ALL logging to stderr. Stdout is reserved for MCP JSON-RPC transport.
    builder.Logging.ClearProviders().AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });

    UserConfigLoader.AddUserConfig(builder.Configuration);
    builder.Configuration.AddJsonFile("qdrant-skills.json", optional: true, reloadOnChange: false);
    ApplyQdrantProtocolFlags(builder, args);
    builder.Services.AddQdrantSkillsInfrastructure(builder.Configuration);

    // MCP server with stdio transport -- tools auto-discovered from this assembly
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    await builder.Build().RunAsync();
}

// --- Shared helpers ---

/// <summary>
/// Applies --qdrant-grpc / --qdrant-http CLI flags to configuration.
/// </summary>
static void ApplyQdrantProtocolFlags(HostApplicationBuilder builder, string[] args)
{
    if (args.Contains("--qdrant-grpc"))
    {
        builder.Configuration["QdrantSkills:QdrantProtocol"] = "grpc";
    }
    else if (args.Contains("--qdrant-http"))
    {
        builder.Configuration["QdrantSkills:QdrantProtocol"] = "http";
    }
}
