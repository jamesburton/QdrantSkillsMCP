using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using QdrantSkillsMCP.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// CRITICAL: ALL logging to stderr. Stdout is reserved for MCP JSON-RPC transport.
// This is the #1 risk identified in research -- stdout pollution breaks MCP.
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Configuration: supports appsettings.json, environment variables, and command-line args.
// Standard .NET precedence: CLI > env > file.
// Also support qdrant-skills.json as alternative portable config file name.
builder.Configuration.AddJsonFile("qdrant-skills.json", optional: true, reloadOnChange: false);

// Register all infrastructure services (Qdrant, embedding, session tracker, parser)
builder.Services.AddQdrantSkillsInfrastructure(builder.Configuration);

// MCP server with stdio transport -- tools auto-discovered from this assembly
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
