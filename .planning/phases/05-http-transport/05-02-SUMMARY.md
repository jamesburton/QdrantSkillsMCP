---
phase: 05-http-transport
plan: 02
subsystem: transport
tags: [http, sse, streamable-http, health-check, cors, kestrel, transport-flags]
dependency_graph:
  requires: [05-01]
  provides: [http-transport-branch, health-endpoints, transport-flag-parsing, url-config]
  affects: [Program.cs, QdrantSkillsOptions]
tech_stack:
  added: [ModelContextProtocol.AspNetCore.WithHttpTransport, ASP.NET Core HealthChecks, CORS middleware]
  patterns: [5-way-branch-routing, flag-conflict-detection, layered-url-resolution, degraded-health-pattern]
key_files:
  created:
    - src/QdrantSkillsMCP.Infrastructure/Health/QdrantHealthCheck.cs
    - src/QdrantSkillsMCP.Infrastructure/Health/HealthResponseWriter.cs
    - src/QdrantSkillsMCP.Infrastructure/Transport/TransportFlags.cs
    - tests/QdrantSkillsMCP.UnitTests/Health/QdrantHealthCheckTests.cs
    - tests/QdrantSkillsMCP.UnitTests/Transport/TransportFlagTests.cs
  modified:
    - src/QdrantSkillsMCP.Infrastructure/Program.cs
    - src/QdrantSkillsMCP.Infrastructure/Configuration/QdrantSkillsOptions.cs
decisions:
  - "EnableLegacySse=true with pragma suppress MCP9004 -- intentional per D-01 for backwards compatibility"
  - "TransportFlags as internal static class with InternalsVisibleTo for unit test access"
  - "QdrantHealthCheck returns Degraded (not Unhealthy) when Qdrant is down per D-07"
  - "CORS permissive (AllowAnyOrigin) for v1.1, to be tightened in future phase"
metrics:
  duration: 5min
  completed: "2026-03-31T11:50:13Z"
  tasks: 3
  files: 7
  tests_added: 22
  tests_total: 271
---

# Phase 5 Plan 2: HTTP Transport Branch, Health Endpoints, and Transport Flags Summary

HTTP transport branch in Program.cs with Streamable HTTP + legacy SSE via MapMcp(), health endpoints at /health and /health/json, transport flag conflict detection, configurable listen URL with 4-level precedence, and 22 new unit tests covering all flag parsing and health check behaviors.

## Task Results

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | QdrantHealthCheck, HealthResponseWriter, Url config | d5d6d70 | Health/QdrantHealthCheck.cs, Health/HealthResponseWriter.cs, QdrantSkillsOptions.cs |
| 2 | Unit tests for health check, transport flags, URL resolution (TDD) | d6c0728 | TransportFlagTests.cs, QdrantHealthCheckTests.cs, TransportFlags.cs |
| 3 | HTTP transport branch in Program.cs | c7d33cc | Program.cs (5-way branch with HTTP transport) |

## What Was Built

1. **QdrantHealthCheck** -- IHealthCheck that returns Degraded (not Unhealthy) when Qdrant is unreachable, keeping the server live for non-Qdrant operations.

2. **HealthResponseWriter** -- Static JSON response writer for /health/json with per-check status, duration, description, and exception details.

3. **TransportFlags** -- Internal static helper for parsing --http, --stdio, --url flags; detecting conflicts; resolving listen URL from 4-level precedence (flag > env > config > default); and stripping transport flags before passing to WebApplication.CreateBuilder.

4. **HTTP transport branch in Program.cs** -- 5-way routing: --config, --console, --setup, --http/--url, default stdio. HTTP branch uses WebApplication.CreateBuilder with WithHttpTransport(EnableLegacySse=true), MapMcp(), health checks, CORS, and Kestrel tuning (2hr KeepAlive, 5min RequestHeaders).

5. **Url config property** -- Added to QdrantSkillsOptions for layered config integration (env var: QDRANT_SKILLS__Url).

## Verification

- `dotnet build` exits 0 (3 warnings, 0 errors)
- `dotnet test` passes 271 tests (249 existing + 22 new), 0 failures
- Program.cs has 5-way branch with transport conflict detection at top
- HTTP branch contains WebApplication.CreateBuilder, WithHttpTransport, MapMcp, MapHealthChecks, AddCors, ConfigureKestrel
- Stdio branch unchanged (Host.CreateApplicationBuilder)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Missing Xunit using directive in test files**
- **Found during:** Task 2
- **Issue:** Test files missing `using Xunit;` causing compilation failures
- **Fix:** Added `using Xunit;` to both test files
- **Files modified:** QdrantHealthCheckTests.cs, TransportFlagTests.cs

**2. [Rule 1 - Bug] MCP9004 obsolete warning for EnableLegacySse**
- **Found during:** Task 3
- **Issue:** EnableLegacySse marked obsolete in MCP SDK 1.2.0, generating build warning
- **Fix:** Added #pragma warning disable/restore MCP9004 with comment explaining intentional use per D-01
- **Files modified:** Program.cs

**3. [Rule 3 - Blocking] Git worktree source control query error**
- **Found during:** Task 3
- **Issue:** `Microsoft.Build.Tasks.Git` fails in worktree with "Found invalid data while decoding"
- **Fix:** Used `-p:EnableSourceControlManagerQueries=false -p:EnableSourceLink=false` for build/test verification
- **Files modified:** None (build-time workaround only)

## Known Stubs

None -- all code is fully wired and functional.

## Self-Check: PASSED

All 7 files verified present. All 3 commit hashes verified in git log.
