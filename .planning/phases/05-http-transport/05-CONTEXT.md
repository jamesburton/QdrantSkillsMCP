# Phase 5: HTTP Transport - Context

**Gathered:** 2026-03-31
**Status:** Ready for planning

<domain>
## Phase Boundary

Add multi-transport support to QdrantSkillsMCP: a single `--http` flag serves both Streamable HTTP and legacy SSE (via `MapMcp()` with `EnableLegacySse=true`). `--stdio` remains the default. `--url` implies HTTP mode and sets the listen address. Conflicting transport flags error and exit. Existing stdio, console, config, and setup modes are untouched.

</domain>

<decisions>
## Implementation Decisions

### Transport Flag Design
- **D-01:** Single `--http` flag serves both Streamable HTTP and legacy SSE (no separate `--sse` vs `--streamable-http` flags). `MapMcp()` with `EnableLegacySse=true` handles both protocols from one code path.
- **D-02:** `--url {address}` implies HTTP mode and sets the Kestrel listen address. e.g. `--url http://0.0.0.0:8080`.
- **D-03:** `--stdio` is an explicit flag (also the default when no transport flag is given).
- **D-04:** Conflicting transport flags (e.g. `--stdio --http`) print an error message and exit with non-zero code. No precedence — fail fast.

### Default Port and URL Binding
- **D-05:** Default HTTP port is 8080. Standard non-privileged port, avoids macOS AirPlay 5000/5001 conflict, works in containers.
- **D-06:** Listen URL is part of the layered config system as `QDRANT_SKILLS_URL` (env var) / `url` (config key). Precedence: `--url` flag > env var > project config > user config > default `http://localhost:8080`.

### Health Endpoint
- **D-07:** `/health` returns quick liveness with degraded status — 200 OK when running, includes "degraded" status if Qdrant connectivity check fails (not 503, still alive).
- **D-08:** `/health/json` returns full health check details (JSON) including individual check statuses, durations, etc.
- **D-09:** Neither health endpoint requires authentication — exempted from auth pipeline (container orchestrator probes can't present tokens).

### Packaging Strategy
- **D-10:** Investigate `PackAsTool=true` + `<FrameworkReference Include="Microsoft.AspNetCore.App" />` as the first task. Goal: keep all transport modes available in both NuGet tool and Docker. If it works, no fallback needed.
- **D-11:** If FrameworkReference breaks PackAsTool, explore minimal alternatives before splitting projects. User wants minimal fracturing — avoid separate server project if possible.
- **D-12:** Update existing Dockerfile (single Dockerfile with `--http` as default entrypoint, overridable to `--stdio` via CMD).

### Claude's Discretion
- CORS configuration details (permissive for v1.1, tighten later)
- Kestrel KeepAliveTimeout value (research suggests 2 hours for long SSE)
- HTTP branch structure in Program.cs (fifth branch using `WebApplication.CreateBuilder`)
- Specific ASP.NET Core health check implementation pattern

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### MCP SDK
- `.planning/research/STACK.md` — ModelContextProtocol 1.2.0 + ModelContextProtocol.AspNetCore 1.2.0 package details
- `.planning/research/ARCHITECTURE.md` — Transport layer architecture, builder branching, `MapMcp()` usage

### MCP Spec
- `.planning/research/FEATURES.md` §HTTP Transports — Streamable HTTP and legacy SSE behavior details
- `.planning/research/PITFALLS.md` §HTTP Transport Pitfalls — stdout contamination, Kestrel timeout drops

### Project
- `.planning/research/SUMMARY.md` — Full synthesis with phase ordering and risk flags
- `.planning/REQUIREMENTS.md` §HTTP Transport — TRANS-01 through TRANS-10

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ServiceRegistration.cs`: `AddQdrantSkillsInfrastructure()` and `AddSetupServices()` — HTTP branch will call `AddQdrantSkillsInfrastructure()` same as stdio
- `ConfigManager` + `UserConfigLoader`: Layered config system — URL config key integrates here
- Existing `Dockerfile`: Base to modify for HTTP entrypoint

### Established Patterns
- **Mode branching in Program.cs**: 4-way `if/else` on `--config`, `--console`, `--setup`, default stdio. HTTP becomes a 5th branch.
- **Builder pattern**: `Host.CreateApplicationBuilder(args)` → HTTP branch will use `WebApplication.CreateBuilder(args)` instead
- **MCP registration**: `.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()` → HTTP changes to `.WithHttpTransport()` 
- **Logging to stderr**: Stdio branch forces all logging to stderr. HTTP branch can use normal stdout logging.

### Integration Points
- `Program.cs:12-69`: Mode branching — new `--http` / `--url` branch added here
- `QdrantSkillsMCP.Infrastructure.csproj`: Package refs (add ModelContextProtocol.AspNetCore, FrameworkReference) and PackAsTool validation
- `ConfigManager`: Add `url` as a configurable key for layered config integration

</code_context>

<specifics>
## Specific Ideas

- Health endpoint should show "degraded" not "unhealthy" when Qdrant is down — the server is still live and could serve cached/non-Qdrant operations
- `/health/json` for detailed check info (timestamps, individual check results) — useful for ops dashboards
- User wants all modes available in both NuGet and Docker if possible — avoid fracturing

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 05-http-transport*
*Context gathered: 2026-03-31*
