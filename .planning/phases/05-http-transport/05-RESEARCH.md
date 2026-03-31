# Phase 5: HTTP Transport - Research

**Researched:** 2026-03-31
**Domain:** ASP.NET Core HTTP transports for MCP server (.NET 10, ModelContextProtocol C# SDK)
**Confidence:** HIGH

## Summary

Phase 5 adds HTTP transport to an existing stdio-only MCP server. The ModelContextProtocol.AspNetCore 1.2.0 package (published 2026-03-27) provides `WithHttpTransport()` and `MapMcp()` which serve both Streamable HTTP and legacy SSE from a single code path. The user has decided on a single `--http` flag (not separate `--sse`/`--streamable-http`), with `--url {address}` implying HTTP mode.

The main technical risk is `PackAsTool=true` + `<FrameworkReference Include="Microsoft.AspNetCore.App" />` compatibility. This MUST be validated first -- if `dotnet pack` breaks, the packaging strategy needs rethinking before any other work proceeds. The project already uses `CreateRidSpecificToolPackages=false` which should help, but it is untested.

**Primary recommendation:** Task 1 validates PackAsTool+FrameworkReference. Task 2 adds the HTTP branch to Program.cs. Task 3 adds health endpoints. Task 4 updates Dockerfile. Task 5 adds `url` to layered config. Regression test stdio after every change.

<user_constraints>

## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Single `--http` flag serves both Streamable HTTP and legacy SSE (no separate `--sse` vs `--streamable-http` flags). `MapMcp()` with `EnableLegacySse=true` handles both protocols from one code path.
- **D-02:** `--url {address}` implies HTTP mode and sets the Kestrel listen address. e.g. `--url http://0.0.0.0:8080`.
- **D-03:** `--stdio` is an explicit flag (also the default when no transport flag is given).
- **D-04:** Conflicting transport flags (e.g. `--stdio --http`) print an error message and exit with non-zero code. No precedence -- fail fast.
- **D-05:** Default HTTP port is 8080.
- **D-06:** Listen URL is part of the layered config system as `QDRANT_SKILLS_URL` (env var) / `url` (config key). Precedence: `--url` flag > env var > project config > user config > default `http://localhost:8080`.
- **D-07:** `/health` returns quick liveness with degraded status -- 200 OK when running, includes "degraded" status if Qdrant connectivity check fails.
- **D-08:** `/health/json` returns full health check details (JSON) including individual check statuses, durations.
- **D-09:** Neither health endpoint requires authentication.
- **D-10:** Investigate `PackAsTool=true` + `<FrameworkReference Include="Microsoft.AspNetCore.App" />` as the first task.
- **D-11:** If FrameworkReference breaks PackAsTool, explore minimal alternatives before splitting projects.
- **D-12:** Update existing Dockerfile with `--http` as default entrypoint, overridable to `--stdio` via CMD.

### Claude's Discretion
- CORS configuration details (permissive for v1.1, tighten later)
- Kestrel KeepAliveTimeout value (research suggests 2 hours for long SSE)
- HTTP branch structure in Program.cs (fifth branch using `WebApplication.CreateBuilder`)
- Specific ASP.NET Core health check implementation pattern

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope.

</user_constraints>

<phase_requirements>

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TRANS-01 | Streamable HTTP transport (POST/GET/DELETE /) | `MapMcp()` in ModelContextProtocol.AspNetCore 1.2.0 registers these endpoints automatically. Single `--http` flag per D-01. |
| TRANS-02 | Legacy SSE transport (GET /sse, POST /message) with EnableLegacySse=true | Same `MapMcp()` call with `EnableLegacySse = true` in `HttpServerTransportOptions`. Single `--http` flag per D-01. |
| TRANS-03 | Explicit --stdio flag, default when no transport flag | Fifth branch in Program.cs; existing default `else` block becomes `--stdio` explicit + default. |
| TRANS-04 | /health endpoint for container liveness probes | ASP.NET Core `AddHealthChecks()` + `MapHealthChecks()` with custom `IHealthCheck` for Qdrant. |
| TRANS-05 | CORS middleware for browser-based MCP clients | `builder.Services.AddCors()` + `app.UseCors()` before auth middleware. Permissive for v1.1. |
| TRANS-06 | Kestrel KeepAliveTimeout (2 hours) for long SSE | `builder.WebHost.ConfigureKestrel()` with `KeepAliveTimeout = TimeSpan.FromHours(2)`. |
| TRANS-07 | Configurable listen URL/port via --url, env var, config | Add `Url` property to config; `QDRANT_SKILLS_URL` env var; `--url` CLI flag; default `http://localhost:8080`. |
| TRANS-08 | dotnet pack with PackAsTool=true + FrameworkReference produces valid NuGet | Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to csproj. Validate `dotnet pack` succeeds and tool installs. |
| TRANS-09 | Existing stdio mode works identically (no regressions) | Mutually exclusive builder branches in Program.cs. Integration test stdio after HTTP additions. |
| TRANS-10 | Dockerfile with EXPOSE, --http entrypoint, env var placeholders | Update existing Dockerfile: `EXPOSE 8080`, `ENTRYPOINT [..., "--http"]`, add URL env vars. |

</phase_requirements>

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `ModelContextProtocol` | 1.2.0 | MCP server core + DI | Upgrade from 1.1.0. Published 2026-03-27. Required by AspNetCore 1.2.0. |
| `ModelContextProtocol.AspNetCore` | 1.2.0 | HTTP transport (`WithHttpTransport`, `MapMcp`) | Published 2026-03-27. Provides Streamable HTTP + legacy SSE from one `MapMcp()` call. |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| ASP.NET Core shared framework | 10.0 (via FrameworkReference) | Kestrel, routing, health checks, CORS | Added via `<FrameworkReference Include="Microsoft.AspNetCore.App" />` -- no NuGet package needed. |

### What NOT to Add

| Package | Why NOT |
|---------|---------|
| `Microsoft.AspNetCore.App` (PackageReference) | Use FrameworkReference, not PackageReference. PackageReference bundles DLLs; FrameworkReference resolves against installed runtime. |
| `Microsoft.Identity.Web` | Auth is Phase 6. Do not add in Phase 5. |
| `Swashbuckle` / `NSwag` | MCP endpoints are not REST APIs. OpenAPI docs would be misleading. |
| `Yarp.ReverseProxy` | Not needed -- Kestrel serves directly. Reverse proxy is infrastructure concern. |

**Installation (csproj changes):**
```xml
<!-- Upgrade -->
<PackageReference Include="ModelContextProtocol" Version="1.2.0" />

<!-- Add -->
<PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.2.0" />

<!-- Add framework reference (NOT a PackageReference) -->
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

**Version verification:**
- ModelContextProtocol 1.2.0: Published 2026-03-27, NuGet verified
- ModelContextProtocol.AspNetCore 1.2.0: Published 2026-03-27, NuGet verified
- Both target net8.0, net9.0, net10.0 (compatible with project's net10.0)

## Architecture Patterns

### Recommended Program.cs Structure (5-way branch)

```
Program.cs branching:
  --config       -> ConfigManager (lightweight, no DI)     [unchanged]
  --console      -> Host.CreateApplicationBuilder           [unchanged]
  --setup        -> Host.CreateApplicationBuilder           [unchanged]
  --http / --url -> WebApplication.CreateBuilder + MapMcp   [NEW]
  default/--stdio -> Host.CreateApplicationBuilder + stdio  [unchanged, now explicit --stdio too]
```

### Pattern 1: Transport Flag Parsing

**What:** Parse `--http`, `--url`, `--stdio` from args, detect conflicts, select builder.
**When to use:** Top of Program.cs, before any builder creation.

```csharp
// Transport flag detection
bool hasHttp = args.Contains("--http");
bool hasUrl = args.Any(a => a == "--url" || a.StartsWith("--url="));
bool hasStdio = args.Contains("--stdio");

bool wantsHttp = hasHttp || hasUrl;
bool wantsStdio = hasStdio;

// Conflict detection
if (wantsHttp && wantsStdio)
{
    Console.Error.WriteLine("Error: --http/--url and --stdio are mutually exclusive.");
    Environment.Exit(1);
}
```

### Pattern 2: HTTP Branch with WebApplication.CreateBuilder

**What:** ASP.NET Core web host with MCP HTTP transport, health checks, CORS, Kestrel tuning.
**When to use:** When `--http` or `--url` flag detected.

```csharp
// Source: ModelContextProtocol.AspNetCore 1.2.0 docs + ASP.NET Core patterns
else if (wantsHttp)
{
    var builder = WebApplication.CreateBuilder(args);

    // Determine listen URL: --url flag > env > config > default
    var listenUrl = ResolveListenUrl(args, builder.Configuration);
    builder.WebHost.UseUrls(listenUrl);

    // Kestrel tuning for long-lived SSE connections
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.KeepAliveTimeout = TimeSpan.FromHours(2);
        options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
    });

    builder.Logging.ClearProviders().AddConsole();
    UserConfigLoader.AddUserConfig(builder.Configuration);
    builder.Configuration.AddJsonFile("qdrant-skills.json", optional: true, reloadOnChange: false);
    builder.Services.AddQdrantSkillsInfrastructure(builder.Configuration);

    // CORS (permissive for v1.1)
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    // Health checks
    builder.Services.AddHealthChecks()
        .AddCheck<QdrantHealthCheck>("qdrant");

    // MCP with HTTP transport
    builder.Services
        .AddMcpServer()
        .WithHttpTransport(options =>
        {
            options.EnableLegacySse = true;  // backward compat for Claude Desktop, older clients
        })
        .WithToolsFromAssembly();

    var app = builder.Build();
    app.UseCors();
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/json", new() { ResponseWriter = WriteDetailedHealthResponse });
    app.MapMcp();
    await app.RunAsync();
}
```

### Pattern 3: Health Check with Degraded Status

**What:** Custom `IHealthCheck` that returns Degraded (not Unhealthy) when Qdrant is unreachable.
**When to use:** `/health` endpoint per D-07.

```csharp
// Source: ASP.NET Core health checks docs (Microsoft Learn, updated 2026-02-25)
public class QdrantHealthCheck : IHealthCheck
{
    private readonly QdrantClient _client;

    public QdrantHealthCheck(QdrantClient client) => _client = client;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await _client.HealthAsync(ct);
            return HealthCheckResult.Healthy("Qdrant reachable");
        }
        catch (Exception ex)
        {
            // Degraded, NOT Unhealthy -- server is still live
            return HealthCheckResult.Degraded("Qdrant unreachable", ex);
        }
    }
}
```

### Pattern 4: URL Resolution from Layered Config

**What:** Resolve listen URL from `--url` flag > `QDRANT_SKILLS_URL` env > config > default.
**When to use:** HTTP branch, before `builder.WebHost.UseUrls()`.

```csharp
static string ResolveListenUrl(string[] args, IConfiguration config)
{
    // 1. --url flag (highest priority)
    var urlIndex = Array.IndexOf(args, "--url");
    if (urlIndex >= 0 && urlIndex + 1 < args.Length)
        return args[urlIndex + 1];

    // 2. QDRANT_SKILLS_URL env var
    var envUrl = Environment.GetEnvironmentVariable("QDRANT_SKILLS_URL");
    if (!string.IsNullOrEmpty(envUrl))
        return envUrl;

    // 3. Config system (project > user)
    var configUrl = config.GetSection("QdrantSkills")["Url"];
    if (!string.IsNullOrEmpty(configUrl))
        return configUrl;

    // 4. Default
    return "http://localhost:8080";
}
```

### Anti-Patterns to Avoid

- **Sharing builder between stdio and HTTP:** `Host.CreateApplicationBuilder` and `WebApplication.CreateBuilder` are different types. Never try to use one for both. Branch early.
- **Unconditional Kestrel startup:** Adding ASP.NET Core references is fine, but calling `WebApplication.CreateBuilder` in stdio mode would start Kestrel and break stdio transport.
- **Using `ASPNETCORE_URLS` for the default:** This env var is standard ASP.NET Core but bypasses the layered config system. Use `QDRANT_SKILLS_URL` to stay consistent with existing `QDRANT_SKILLS__*` pattern. The HTTP branch can set `ASPNETCORE_URLS` internally from the resolved URL.
- **Returning 503 from health when Qdrant is down:** Per D-07, use Degraded (200 OK), not Unhealthy (503). The server is alive; Qdrant is optional.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Streamable HTTP endpoints | Custom POST/GET/DELETE handlers | `MapMcp()` from ModelContextProtocol.AspNetCore | SDK handles session management, SSE streaming, Mcp-Session-Id headers, backpressure |
| Legacy SSE endpoints | Custom /sse and /message handlers | `MapMcp()` with `EnableLegacySse = true` | SDK handles SSE event format, connection lifecycle |
| Health checks | Custom `/health` middleware | ASP.NET Core `AddHealthChecks()` + `MapHealthChecks()` | Standard pattern, supports custom writers, integrates with Container Apps probes |
| CORS | Custom middleware | `AddCors()` + `UseCors()` | Handles preflight OPTIONS, exposed headers, SSE-compatible |
| Kestrel timeout config | Custom connection management | `ConfigureKestrel()` options | Built-in, well-tested |

**Key insight:** The ModelContextProtocol.AspNetCore package does ALL the heavy lifting for MCP-over-HTTP. `MapMcp()` is a single call that registers both transport types. The phase work is primarily wiring (Program.cs branching, CLI flag parsing, config integration), not protocol implementation.

## Common Pitfalls

### Pitfall 1: stdout contamination breaks stdio after adding HTTP dependencies
**What goes wrong:** Adding `ModelContextProtocol.AspNetCore` reference and ASP.NET Core FrameworkReference to the project. If any startup code path accidentally initializes Kestrel in stdio mode, stdout gets polluted.
**Why it happens:** `WebApplication.CreateBuilder()` automatically configures Kestrel. If it runs in the stdio branch, Kestrel writes to stdout.
**How to avoid:** Keep builder branches mutually exclusive. stdio uses `Host.CreateApplicationBuilder()`. HTTP uses `WebApplication.CreateBuilder()`. The `if/else if/else` structure in Program.cs already enforces this -- maintain it.
**Warning signs:** stdio mode stops working; MCP clients get parse errors; Kestrel startup banner appears on stdout.

### Pitfall 2: PackAsTool + FrameworkReference interaction
**What goes wrong:** `dotnet pack` might fail or produce a broken NuGet tool package when `<FrameworkReference Include="Microsoft.AspNetCore.App" />` is combined with `<PackAsTool>true</PackAsTool>`.
**Why it happens:** FrameworkReference tells the runtime to resolve ASP.NET Core from the shared framework. For tool packages, the aspnet runtime must be installed. If the tool package metadata is wrong, `dotnet tool install` fails.
**How to avoid:** Validate this FIRST (D-10). Run `dotnet pack`, install the tool from the local nupkg, run it with `--http` and `--stdio`. If it breaks, investigate `CreateRidSpecificToolPackages=false` interaction or consider conditional compilation.
**Warning signs:** `dotnet pack` warnings about framework references; tool install fails with "framework not found"; tool runs but HTTP mode throws TypeLoadException.

### Pitfall 3: EnableLegacySse default changed in 1.2.0
**What goes wrong:** Upgrade ModelContextProtocol from 1.1.0 to 1.2.0. The `EnableLegacySse` property now defaults to `false`. Legacy SSE clients (Claude Desktop, etc.) get 404 on `/sse`.
**Why it happens:** SDK 1.2.0 breaking change -- SSE is deprecated, disabled by default.
**How to avoid:** Explicitly set `options.EnableLegacySse = true` in `WithHttpTransport()`. This is already in the plan per D-01.
**Warning signs:** `/sse` returns 404; SSE-only clients can't connect; Streamable HTTP works fine.

### Pitfall 4: Kestrel KeepAliveTimeout kills SSE connections
**What goes wrong:** Default Kestrel `KeepAliveTimeout` is 2 minutes. Long-lived SSE connections drop after ~2 minutes.
**Why it happens:** Kestrel treats idle connections as timed out.
**How to avoid:** Set `KeepAliveTimeout = TimeSpan.FromHours(2)` in `ConfigureKestrel()`. Also set `RequestHeadersTimeout = TimeSpan.FromMinutes(5)`.
**Warning signs:** SSE connections drop after ~2 minutes; clients reconnect in a loop.

### Pitfall 5: --url flag not removing itself from args passed to builder
**What goes wrong:** `--url http://0.0.0.0:8080` is passed through to `WebApplication.CreateBuilder(args)`. ASP.NET Core does not recognize `--url` as a standard flag and may warn or error.
**Why it happens:** Custom CLI flags mixed with ASP.NET Core's built-in arg parsing.
**How to avoid:** Strip `--url` and its value from args before passing to `WebApplication.CreateBuilder()`. Or use `--urls` which IS a recognized ASP.NET Core flag (maps to `ASPNETCORE_URLS`). Decision needed: use `--url` (custom, strip from args) or `--urls` (standard ASP.NET Core flag, works natively). Recommendation: use `--url` (matches CONTEXT.md D-02) and strip it from args, then call `builder.WebHost.UseUrls(resolvedUrl)`.

### Pitfall 6: Health endpoint returning wrong HTTP status for Degraded
**What goes wrong:** ASP.NET Core `MapHealthChecks` by default maps `Degraded` to HTTP 200, `Unhealthy` to 503. But custom `ResultStatusCodes` might accidentally map Degraded to 500.
**Why it happens:** Misconfiguring `HealthCheckOptions.ResultStatusCodes`.
**How to avoid:** Use default status codes (200 for Healthy/Degraded, 503 for Unhealthy) which matches D-07's "200 OK when running, includes degraded status" requirement. Do not customize `ResultStatusCodes`.
**Warning signs:** Container orchestrator restarts container when Qdrant is down because health returns 5xx.

## Code Examples

### Complete csproj Changes

```xml
<!-- In QdrantSkillsMCP.Infrastructure.csproj -->

<!-- Upgrade existing -->
<PackageReference Include="ModelContextProtocol" Version="1.2.0" />

<!-- Add new -->
<PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.2.0" />

<!-- Add FrameworkReference (NOT inside <ItemGroup> with PackageReferences -- separate ItemGroup) -->
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

### Health Check Response Writer for /health/json

```csharp
// Source: ASP.NET Core health checks docs
static Task WriteDetailedHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    var result = new
    {
        status = report.Status.ToString(),
        duration = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            duration = e.Value.Duration.TotalMilliseconds,
            exception = e.Value.Exception?.Message
        })
    };
    return context.Response.WriteAsJsonAsync(result);
}
```

### Dockerfile Update

```dockerfile
# Updated entrypoint and port
EXPOSE 8080

ENV ASPNETCORE_ENVIRONMENT=Production \
    QDRANT_SKILLS_URL=http://+:8080 \
    QdrantSkills__QdrantHost=qdrant \
    QdrantSkills__QdrantGrpcPort=6334 \
    QdrantSkills__CollectionName=skills \
    QdrantSkills__EmbeddingProvider=openai

# Default to HTTP in container; override with CMD ["--stdio"] if needed
ENTRYPOINT ["dotnet", "QdrantSkillsMCP.Infrastructure.dll", "--http"]
```

### Config System Integration (QdrantSkillsOptions addition)

```csharp
// Add to QdrantSkillsOptions.cs
/// <summary>Listen URL for HTTP transport. Default: http://localhost:8080.</summary>
public string? Url { get; set; }
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| SSE-only transport (/sse + /message) | Streamable HTTP (POST/GET/DELETE /) + optional legacy SSE | MCP spec 2025-03-26 | SSE deprecated; Streamable HTTP is primary. SDK 1.2.0 disables SSE by default. |
| `McpServerBuilder.WithSseTransport()` | `WithHttpTransport()` + `MapMcp()` | SDK 1.0.0 (early 2026) | Single method serves both transport types. |
| Separate HTTP server project | FrameworkReference in console app | Always available, now standard pattern | `PackAsTool` console app can conditionally use ASP.NET Core without changing SDK. |

**Deprecated/outdated:**
- `EnableLegacySse` property is marked `[Obsolete]` in SDK 1.2.0 but functional. Will be removed in a future major version.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 |
| Config file | Directory.Build.props (TargetFramework: net10.0) |
| Quick run command | `dotnet test tests/QdrantSkillsMCP.UnitTests -x --verbosity quiet` |
| Full suite command | `dotnet test --verbosity quiet` |

### Phase Requirements -> Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| TRANS-01 | Streamable HTTP endpoints respond | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "HttpTransport"` | Wave 0 |
| TRANS-02 | Legacy SSE /sse endpoint responds | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "LegacySse"` | Wave 0 |
| TRANS-03 | --stdio works identically to v1.0 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "StdioRegression"` | Wave 0 |
| TRANS-04 | /health returns liveness | unit + integration | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "HealthCheck"` | Wave 0 |
| TRANS-05 | CORS headers present on HTTP responses | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "Cors"` | Wave 0 |
| TRANS-06 | Kestrel KeepAliveTimeout configured | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "KestrelConfig"` | Wave 0 |
| TRANS-07 | URL configurable via --url, env, config | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "UrlConfig"` | Wave 0 |
| TRANS-08 | dotnet pack produces valid NuGet tool | manual | `dotnet pack && dotnet tool install --global --add-source ./nupkg QdrantSkillsMCP` | manual-only (requires global tool install) |
| TRANS-09 | stdio regression | integration | Same as TRANS-03 | Wave 0 |
| TRANS-10 | Docker builds and runs with --http | manual | `docker build -t test . && docker run --rm test --help` | manual-only (requires Docker) |

### Sampling Rate
- **Per task commit:** `dotnet test tests/QdrantSkillsMCP.UnitTests -x --verbosity quiet`
- **Per wave merge:** `dotnet test --verbosity quiet`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/QdrantSkillsMCP.UnitTests/Health/QdrantHealthCheckTests.cs` -- covers TRANS-04
- [ ] `tests/QdrantSkillsMCP.UnitTests/Transport/TransportFlagTests.cs` -- covers TRANS-03, TRANS-07 (flag parsing, conflict detection, URL resolution)
- [ ] `tests/QdrantSkillsMCP.UnitTests/Transport/KestrelConfigTests.cs` -- covers TRANS-06

Note: Full HTTP integration tests (TRANS-01, TRANS-02, TRANS-05) require `WebApplicationFactory<T>` or in-process hosting which depends on the HTTP branch existing first. These are created alongside their implementation tasks, not in Wave 0.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build | Yes | 10.0.201 | -- |
| ASP.NET Core runtime | HTTP transport | Yes (via FrameworkReference) | 10.0 | -- |
| Docker | TRANS-10 | Needs verification | -- | Manual Dockerfile review |

## Open Questions

1. **PackAsTool + FrameworkReference: Will it work?**
   - What we know: FrameworkReference is the standard pattern for console apps using ASP.NET Core. PackAsTool is standard for .NET tools. The project has `CreateRidSpecificToolPackages=false`.
   - What's unclear: Whether the combination produces a valid tool package that installs and runs on a machine with only the aspnet runtime (not the SDK).
   - Recommendation: First task must validate this. If broken, fallback options: (a) conditional compilation with `#if` to exclude ASP.NET types from pack, (b) separate project for HTTP server, (c) runtime assembly loading.

2. **`--url` flag stripping from args**
   - What we know: `--url` is not a standard ASP.NET Core CLI flag. `--urls` is.
   - What's unclear: Whether passing `--url http://...` to `WebApplication.CreateBuilder(args)` causes warnings or errors.
   - Recommendation: Strip custom flags (`--http`, `--url` + value) from args before passing to builder. Simple string filtering.

3. **Health check registration in non-HTTP modes**
   - What we know: `AddHealthChecks()` is an ASP.NET Core service. It's meaningless in stdio mode.
   - What's unclear: Whether registering it in stdio mode causes any overhead or issues.
   - Recommendation: Only register health checks in the HTTP branch. Keep branches clean.

## Project Constraints (from CLAUDE.md)

No CLAUDE.md found in project root. No project-specific constraints beyond standard .NET conventions observed in codebase.

## Sources

### Primary (HIGH confidence)
- [NuGet: ModelContextProtocol 1.2.0](https://www.nuget.org/packages/ModelContextProtocol/) -- version, publish date, dependencies verified
- [NuGet: ModelContextProtocol.AspNetCore 1.2.0](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore/) -- version, publish date, dependencies verified
- [HttpServerTransportOptions API](https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.AspNetCore.HttpServerTransportOptions.html) -- all properties verified
- [MCP C# SDK Getting Started](https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html) -- WithHttpTransport + MapMcp pattern verified
- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0) -- updated 2026-02-25
- [.NET 10 PackAsTool breaking change](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/dotnet-tool-pack-publish) -- CreateRidSpecificToolPackages behavior

### Secondary (MEDIUM confidence)
- [MCP C# SDK GitHub releases](https://github.com/modelcontextprotocol/csharp-sdk/releases) -- 1.2.0 release notes, EnableLegacySse default change
- `.planning/research/STACK.md` -- milestone-level stack research (cross-verified)
- `.planning/research/ARCHITECTURE.md` -- transport layer architecture (cross-verified)
- `.planning/research/PITFALLS.md` -- pitfall catalog (cross-verified)

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- packages verified on NuGet, APIs verified in official docs
- Architecture: HIGH -- Program.cs branching pattern matches existing codebase structure, SDK APIs verified
- Pitfalls: HIGH -- stdout contamination verified from current code, SDK breaking change confirmed in release notes
- PackAsTool + FrameworkReference: MEDIUM -- standard pattern individually, but combination untested in this project

**Research date:** 2026-03-31
**Valid until:** 2026-04-30 (stable -- SDK 1.2.0 is latest stable, no preview versions in use)
