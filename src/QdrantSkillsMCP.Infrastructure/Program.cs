using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using QdrantSkillsMCP.Infrastructure;
using QdrantSkillsMCP.Infrastructure.Cli;
using QdrantSkillsMCP.Infrastructure.Cli.Commands;
using QdrantSkillsMCP.Infrastructure.Configuration;
using QdrantSkillsMCP.Infrastructure.Health;
using QdrantSkillsMCP.Infrastructure.Setup;
using QdrantSkillsMCP.Infrastructure.Transport;

// Transport conflict detection per D-04
if (TransportFlags.HasConflict(args))
{
    Console.Error.WriteLine("Error: --http/--url and --stdio are mutually exclusive.");
    Environment.Exit(1);
}

// Mode branching: --config, --console, --setup, --http/--url, or default MCP server (stdio)
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
    var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { Args = args });
    builder.Configuration.AddEnvironmentVariables();
    builder.Logging.AddConsole();
    UserConfigLoader.AddUserConfig(builder.Configuration);
    builder.Configuration.AddJsonFile("qdrant-skills.json", optional: true, reloadOnChange: false);
    ApplyQdrantProtocolFlags(builder.Configuration, args);
    builder.Services.AddQdrantSkillsInfrastructure(builder.Configuration);

    var host = builder.Build();
    var consoleHost = new ConsoleHost(host.Services);
    var exitCode = await consoleHost.RunAsync(args);
    Environment.Exit(exitCode);
}
else if (args.Contains("--setup"))
{
    // Setup wizard mode: registers only setup services (no Qdrant connection needed)
    var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { Args = args });
    builder.Configuration.AddEnvironmentVariables();
    builder.Logging.AddConsole();
    UserConfigLoader.AddUserConfig(builder.Configuration);
    builder.Services.AddSetupServices();

    var host = builder.Build();
    var wizard = host.Services.GetRequiredService<SetupWizard>();
    var exitCode = await wizard.RunAsync(args);
    Environment.Exit(exitCode);
}
else if (TransportFlags.WantsHttp(args))
{
    // HTTP mode: Streamable HTTP + legacy SSE per D-01
    var cleanArgs = TransportFlags.StripTransportFlags(args);
    var builder = WebApplication.CreateBuilder(cleanArgs);

    // Resolve listen URL per D-06 precedence: --url > env > config > default
    UserConfigLoader.AddUserConfig(builder.Configuration);
    builder.Configuration.AddJsonFile("qdrant-skills.json", optional: true, reloadOnChange: false);

    var envUrl = Environment.GetEnvironmentVariable("QDRANT_SKILLS_URL");
    var configUrl = builder.Configuration.GetSection("QdrantSkills")["Url"];
    var listenUrl = TransportFlags.ResolveListenUrl(args, envUrl, configUrl);
    builder.WebHost.UseUrls(listenUrl);

    // Kestrel tuning for long-lived SSE connections per TRANS-06
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.KeepAliveTimeout = TimeSpan.FromHours(2);
        options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
    });

    builder.Logging.ClearProviders().AddConsole();

    // Apply --qdrant-grpc / --qdrant-http protocol flags
    ApplyQdrantProtocolFlags(builder.Configuration, args);

    builder.Services.AddQdrantSkillsInfrastructure(builder.Configuration);

    // CORS per TRANS-05 (permissive for v1.1, tighten later)
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    // Health checks per TRANS-04 / D-07 / D-08
    builder.Services.AddHealthChecks()
        .AddCheck<QdrantHealthCheck>("qdrant");

    // MCP with HTTP transport -- serves both Streamable HTTP and legacy SSE per D-01
    builder.Services
        .AddMcpServer()
        .WithHttpTransport(options =>
        {
#pragma warning disable MCP9004 // Legacy SSE intentionally enabled per D-01 for backwards compatibility
            options.EnableLegacySse = true;
#pragma warning restore MCP9004
        })
        .WithToolsFromAssembly();

    var app = builder.Build();
    app.UseCors();
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/json", new HealthCheckOptions
    {
        ResponseWriter = HealthResponseWriter.WriteDetailedHealthResponse
    });
    app.MapMcp();
    await app.RunAsync();
}
else
{
    // Default: MCP server mode via stdio (also explicit --stdio per D-03)
    // Use CreateEmptyApplicationBuilder to avoid default EventLog provider registration,
    // which crashes when run via 'dnx' (assembly probing can't find System.Diagnostics.EventLog).
    var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { Args = args });
    builder.Configuration.AddEnvironmentVariables();

    // CRITICAL: ALL logging to stderr. Stdout is reserved for MCP JSON-RPC transport.
    builder.Logging.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });

    UserConfigLoader.AddUserConfig(builder.Configuration);
    builder.Configuration.AddJsonFile("qdrant-skills.json", optional: true, reloadOnChange: false);
    ApplyQdrantProtocolFlags(builder.Configuration, args);
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
/// Accepts IConfigurationManager so it works with both HostApplicationBuilder and WebApplicationBuilder.
/// </summary>
static void ApplyQdrantProtocolFlags(IConfigurationManager config, string[] args)
{
    if (args.Contains("--qdrant-grpc"))
        config["QdrantSkills:QdrantProtocol"] = "grpc";
    else if (args.Contains("--qdrant-http"))
        config["QdrantSkills:QdrantProtocol"] = "http";
}
