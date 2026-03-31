# Architecture Research -- v1.1 Shared Server

**Researched:** 2026-03-31
**Overall confidence:** HIGH (verified against ModelContextProtocol C# SDK docs, official samples, Microsoft.Identity.Web docs)

## Transport Layer

### How stdio / SSE / Streamable HTTP Coexist

The fundamental architectural change is that **Infrastructure's Program.cs must branch between `Host.CreateApplicationBuilder` (stdio) and `WebApplication.CreateBuilder` (HTTP transports)** based on CLI args. These are different builder types -- stdio uses the generic host, HTTP uses the ASP.NET Core web host.

**Key constraint:** `WithStdioServerTransport()` requires `Host.CreateApplicationBuilder`. `WithHttpTransport()` requires `WebApplication.CreateBuilder` + `app.MapMcp()`. They use different host builders, so the branch must happen early in Program.cs.

**Transport selection logic:**

```
--sse              -> WebApplication + WithHttpTransport() + MapMcp()  (legacy SSE endpoints auto-included)
--streamable-http  -> same as above (Streamable HTTP is the default for MapMcp)
--url {URL}        -> same as above, with custom listen URL
(default)          -> Host.CreateApplicationBuilder + WithStdioServerTransport()  (unchanged from v1.0)
--console          -> unchanged
--setup            -> unchanged
--config           -> unchanged
```

**The SDK handles SSE backward compatibility automatically.** When `MapMcp()` is called, it registers both Streamable HTTP endpoints (POST/GET/DELETE at the base path) AND legacy SSE endpoints at `/sse` and `/message`. There is no need for separate SSE vs Streamable HTTP code paths -- `--sse` and `--streamable-http` can use the same `WithHttpTransport()` + `MapMcp()` pipeline. The `--sse` flag exists for user clarity but the implementation is identical.

**Where Kestrel fits:** When `--sse` or `--streamable-http` is specified, the app becomes a Kestrel-hosted ASP.NET Core web server. The Infrastructure project gains a dependency on `ModelContextProtocol.AspNetCore` (which transitively includes ASP.NET Core hosting).

**Recommended approach -- FrameworkReference, not SDK change:**

```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

This is the standard pattern for console apps that conditionally use ASP.NET Core. Changing to `Microsoft.NET.Sdk.Web` would alter publish behavior and break the NuGet tool packaging (`PackAsTool=true`). The FrameworkReference approach keeps `OutputType=Exe` and `PackAsTool=true` intact. The `ModelContextProtocol.AspNetCore` NuGet package provides `WithHttpTransport()` and `MapMcp()`.

### Program.cs HTTP Branch (Sketch)

```csharp
else if (args.Contains("--sse") || args.Contains("--streamable-http") || args.Contains("--url"))
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders().AddConsole();
    UserConfigLoader.AddUserConfig(builder.Configuration);
    builder.Configuration.AddJsonFile("qdrant-skills.json", optional: true, reloadOnChange: false);
    builder.Services.AddQdrantSkillsInfrastructure(builder.Configuration);

    // Auth (only for HTTP mode)
    builder.Services.AddEntraAuthentication(builder.Configuration);

    // MCP with HTTP transport
    builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithToolsFromAssembly();

    var app = builder.Build();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapMcp().RequireAuthorization();
    await app.RunAsync();
}
```

### Session Management Implications

In stdio mode, one process = one client = one session. The `InMemorySessionTracker` singleton works as-is.

In HTTP mode, multiple clients connect concurrently. The MCP SDK handles session multiplexing via `Mcp-Session-Id` headers. Each HTTP session gets its own `IMcpServer` instance. The `InMemorySessionTracker` already supports keyed sessions (by sessionId parameter), so it works for multi-client scenarios IF each MCP tool invocation passes the correct session ID.

**Action needed:** Verify how the MCP SDK exposes session identity to tool classes in HTTP mode. If it provides a session ID via `IMcpServer` or `HttpContext`, the tools need to pass that through to `ISessionTracker`. Current tools use `sessionId: null` which falls back to `__default__` -- this must be updated for HTTP mode to use the MCP session ID.

## Auth Middleware

### Where OAuth Validation Goes

**Standard ASP.NET Core authentication middleware.** This is correct because:

1. The MCP SDK's HTTP transport runs inside ASP.NET Core's request pipeline
2. `MapMcp()` registers standard ASP.NET Core endpoints
3. Authentication middleware runs before endpoint handlers
4. Microsoft.Identity.Web integrates natively with this pipeline

**Pipeline order (HTTP mode only):**

```
Request
  -> UseAuthentication()              // Validates JWT, populates HttpContext.User
  -> UseAuthorization()               // Enforces policies
  -> MapMcp().RequireAuthorization()  // MCP endpoints require valid token
```

### Entra JWT Validation with DI

**Package:** `Microsoft.Identity.Web` (pulls in `Microsoft.AspNetCore.Authentication.JwtBearer`)

**Registration pattern:**

```csharp
public static class AuthRegistration
{
    public static IServiceCollection AddEntraAuthentication(
        this IServiceCollection services, IConfiguration config)
    {
        var azureAdSection = config.GetSection("AzureAd");
        if (!azureAdSection.Exists())
            return services; // No auth configured -- skip (allows unauthenticated local dev)

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(azureAdSection);

        services.AddAuthorization(options =>
        {
            options.AddPolicy("SkillsRead", policy =>
                policy.RequireScope("Skills.Read", "Skills.ReadWrite"));
            options.AddPolicy("SkillsWrite", policy =>
                policy.RequireScope("Skills.Write", "Skills.ReadWrite"));
        });

        return services;
    }
}
```

**Configuration section (new, separate from QdrantSkills section):**

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "{tenant-id}",
    "ClientId": "{client-id}",
    "Audience": "api://{client-id}"
  }
}
```

**Auth is ONLY for HTTP transports.** Stdio mode has no HTTP pipeline, no auth middleware, no JWT validation. The auth registration must be conditional on the transport mode -- achieved by placing it only in the HTTP branch of Program.cs.

### Scope Enforcement Strategy

Two approaches for granular read/write scope enforcement:

1. **Simple (recommended for v1.1):** Single `RequireAuthorization()` on `MapMcp()` requiring any valid token with a `Skills.Read` or `Skills.ReadWrite` scope. All authenticated users can call all tools.

2. **Granular (future):** Different policies per tool type. Requires either a custom `IAuthorizationHandler` that inspects the MCP method name from `HttpContext`, or splitting read/write tools into separate endpoint groups. Over-engineering for v1.1.

Recommendation: Start with approach 1 (any valid scope = full access). Add granular scopes later if needed.

### `az` CLI Persisted Login Support

For developer convenience, tokens obtained via `az login` work out-of-the-box with Microsoft.Identity.Web as long as the token audience matches the configured ClientId/Audience. No special server-side handling needed.

## New Components

### New Files

| File | Project | Purpose |
|------|---------|---------|
| `Auth/AuthRegistration.cs` | Infrastructure | Extension method `AddEntraAuthentication()` -- registers JWT bearer auth + authorization policies. Only called in HTTP transport mode. |
| `infra/main.bicep` | repo root | Orchestrator Bicep template |
| `infra/modules/containerApp.bicep` | repo root | Azure Container Apps environment + app + managed identity |
| `infra/modules/entraApp.bicep` | repo root | Entra app registration, scopes, group assignments |
| `.github/workflows/deploy.yml` | repo root | CI/CD: build, push image to ACR, deploy to ACA |

### New NuGet Package References (Infrastructure.csproj)

| Package | Version | Purpose |
|---------|---------|---------|
| `ModelContextProtocol.AspNetCore` | 1.1.0 | HTTP transport (`WithHttpTransport`, `MapMcp`) |
| `Microsoft.Identity.Web` | latest stable | Entra JWT validation + scope enforcement |

### New Framework Reference (Infrastructure.csproj)

```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

## Modified Components

### Program.cs -- MAJOR MODIFICATION

Current: 4-way branch (`--config`, `--console`, `--setup`, default stdio).
New: 5-way branch adding HTTP transport mode.

**Changes:**
1. Add detection for `--sse`, `--streamable-http`, `--url` args before the default stdio branch
2. New branch uses `WebApplication.CreateBuilder(args)` instead of `Host.CreateApplicationBuilder(args)`
3. New branch calls `AddEntraAuthentication()` + `AddQdrantSkillsInfrastructure()` + `AddMcpServer().WithHttpTransport().WithToolsFromAssembly()`
4. New branch builds pipeline: `UseAuthentication()`, `UseAuthorization()`, `MapMcp().RequireAuthorization()`
5. Existing stdio branch and all other branches: **unchanged**

### QdrantSkillsMCP.Infrastructure.csproj -- MODERATE MODIFICATION

**Changes:**
1. Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (enables ASP.NET Core without changing SDK)
2. Add `<PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.1.0" />`
3. Add `<PackageReference Include="Microsoft.Identity.Web" Version="..." />`

**Validation required:** Confirm `dotnet pack` still produces a valid NuGet tool after adding FrameworkReference. This is the highest-risk change for packaging.

### QdrantSkillsOptions.cs -- MINOR MODIFICATION (or no change)

HTTP transport port/URL can be handled via standard ASP.NET Core configuration (`ASPNETCORE_URLS`, `--urls` CLI arg) rather than a custom property. Auth config uses the standard `AzureAd` section, not QdrantSkillsOptions.

Likely no changes needed to this file. If a `TransportMode` property is desired for programmatic access, add it, but CLI arg detection in Program.cs is simpler and sufficient.

### Dockerfile -- MODERATE MODIFICATION

**Changes:**
1. Add `EXPOSE 3001` (or chosen HTTP port)
2. Change ENTRYPOINT to include `--streamable-http`: `ENTRYPOINT ["dotnet", "QdrantSkillsMCP.Infrastructure.dll", "--streamable-http"]`
3. Add auth-related environment variable placeholders: `AzureAd__TenantId`, `AzureAd__ClientId`, `AzureAd__Instance`
4. Add `HEALTHCHECK` directive (optional but recommended for ACA)

The existing Dockerfile already uses `mcr.microsoft.com/dotnet/aspnet:10.0` as the runtime base image, so ASP.NET Core shared framework is already available. No base image change needed.

### AppHost/Program.cs -- MINOR MODIFICATION

**Changes:**
1. Add `WithHttpEndpoint()` to server project when testing HTTP transport locally
2. Optionally add environment variables to switch transport mode for Aspire debugging

### InMemorySessionTracker.cs -- NO CODE CHANGES NEEDED

Already supports keyed sessions via sessionId parameter. Works for multi-client HTTP scenarios.

### Tool Classes (SkillSearchTools.cs, SkillCrudTools.cs, etc.) -- MINOR MODIFICATION (deferred)

Tools currently pass `sessionId: null` to the session tracker. For HTTP multi-client correctness, they should pass the MCP session ID. This can be deferred to a follow-up if single-session HTTP behavior is acceptable initially.

## Build Order

### Phase 1: HTTP Transport (no auth)

**What:** Add `--sse` / `--streamable-http` CLI branches to Program.cs. Use `WebApplication.CreateBuilder` + `WithHttpTransport()` + `MapMcp()`. No authentication.

**Why first:** Foundation for everything. Auth, Docker updates, and CI/CD all depend on HTTP endpoints existing. Can be developed and tested locally without Azure.

**Deliverables:**
- Modified `Program.cs` with HTTP branch
- Modified `.csproj` with FrameworkReference + ModelContextProtocol.AspNetCore
- Verify `dotnet pack` still produces valid NuGet tool
- Integration test: stdio still works (regression)
- Integration test: HTTP transport serves MCP requests

**Risk:** FrameworkReference + PackAsTool interaction. Test `dotnet pack` early.

### Phase 2: Entra Authentication

**What:** Add JWT bearer auth via Microsoft.Identity.Web. Scope-based authorization. Conditional registration (HTTP only).

**Why second:** Depends on HTTP transport from Phase 1. Can be tested with `az login` tokens against a real Entra tenant.

**Deliverables:**
- `Auth/AuthRegistration.cs` extension method
- `AzureAd` config section support
- Authorization policies for scopes
- Modified Program.cs HTTP branch to wire auth
- Tests with mock JWT validation

### Phase 3: Bicep IaC

**What:** Azure Container Apps + Entra app registration infrastructure as code.

**Why third:** Depends on knowing auth config shape (Phase 2) and container requirements (Phase 1). Independent of CI/CD.

**Deliverables:**
- `infra/main.bicep` orchestrator
- Container App module (environment, app, managed identity, ingress)
- Entra app registration module (client ID, scopes: Skills.Read/Skills.Write/Skills.ReadWrite, group assignments for qhub-people-dev/devops/qa/ba)
- Parameter files

### Phase 4: Docker + CI/CD

**What:** Update Dockerfile for HTTP transport. Create GitHub Actions workflow.

**Why last:** Depends on all prior phases. The Dockerfile needs the final binary shape. CI/CD deploys using Bicep from Phase 3.

**Deliverables:**
- Updated Dockerfile with EXPOSE and --streamable-http entrypoint
- `.github/workflows/deploy.yml` (build -> ACR push -> ACA deploy via Bicep)
- Smoke test / health check validation

### Dependency Graph

```
Phase 1: HTTP Transport
    |
    v
Phase 2: Entra Auth (requires HTTP endpoints to protect)
    |
    v
Phase 3: Bicep IaC (requires auth config shape + container config)
    |
    v
Phase 4: Docker + CI/CD (requires Dockerfile + Bicep + everything)
```

## Docker / Packaging

### Two Distribution Channels, Same Entry Point

1. **NuGet tool (`dnx QdrantSkillsMCP`):** For local/stdio use by developers. Unchanged from v1.0. Users can also run `--sse` locally if desired.

2. **Docker container:** For shared server deployment. Runs with `--streamable-http` by default. Hosted in Azure Container Apps. This is the v1.1 addition.

Both invoke `QdrantSkillsMCP.Infrastructure.dll`. The Dockerfile already exists and uses the correct `aspnet:10.0` base image.

### NuGet Tool Package Size Impact

Adding `FrameworkReference` to `Microsoft.AspNetCore.App` does NOT increase the NuGet tool package because framework references resolve against the installed runtime, not bundled DLLs. `Microsoft.Identity.Web` adds a few hundred KB at most. `ModelContextProtocol.AspNetCore` is lightweight. Acceptable.

### Dockerfile Changes Summary

```dockerfile
# Add port exposure
EXPOSE 3001

# Add auth env vars
ENV AzureAd__Instance="https://login.microsoftonline.com/" \
    AzureAd__TenantId="" \
    AzureAd__ClientId=""

# Default to HTTP transport in container
ENTRYPOINT ["dotnet", "QdrantSkillsMCP.Infrastructure.dll", "--streamable-http"]
```

## Sources

- [ModelContextProtocol.AspNetCore NuGet 1.1.0](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore/) - HIGH confidence
- [MCP C# SDK Getting Started](https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html) - HIGH confidence
- [Streamable HTTP Protocol - DeepWiki](https://deepwiki.com/modelcontextprotocol/csharp-sdk/5.4-streamable-http-protocol) - MEDIUM confidence
- [Building Remote MCP Servers with .NET and Azure Container Apps](https://dev.to/willvelida/building-remote-mcp-servers-with-net-and-azure-container-apps-cc2) - MEDIUM confidence
- [Migration from SSE to Streamable HTTP - GitHub Discussion #790](https://github.com/modelcontextprotocol/csharp-sdk/discussions/790) - HIGH confidence
- [HttpServerTransportOptions API](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.AspNetCore.HttpServerTransportOptions.html) - HIGH confidence
- [MCP Transports Specification 2025-06-18](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports) - HIGH confidence
- [Configure protected web API - Microsoft Identity Platform](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-protected-web-api-app-configuration) - HIGH confidence
- [JWT bearer authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0) - HIGH confidence
