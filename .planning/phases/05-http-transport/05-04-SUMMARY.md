---
phase: 05-http-transport
plan: "04"
subsystem: infra
tags: [grpc, grpc-web, azure, qdrant, protocol-selection]

# Dependency graph
requires:
  - phase: 05-http-transport
    provides: MCP SDK upgrade, HTTP transport branch, TransportFlags helper
provides:
  - QdrantClientFactory with protocol-aware client creation (gRPC, gRPC-Web, HTTP)
  - QdrantProtocol config key and QdrantProtocolType enum
  - --qdrant-grpc and --qdrant-http CLI flags
  - Auto-detection: gRPC for localhost, gRPC-Web for remote (Azure-compatible)
affects: [deployment, docker, azure-hosting]

# Tech tracking
tech-stack:
  added: [Grpc.Net.Client.Web]
  patterns: [factory-pattern-for-qdrant-client, auto-detect-protocol-by-host]

key-files:
  created:
    - src/QdrantSkillsMCP.Infrastructure/Qdrant/QdrantClientFactory.cs
    - tests/QdrantSkillsMCP.UnitTests/Qdrant/QdrantProtocolTests.cs
  modified:
    - src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj
    - src/QdrantSkillsMCP.Infrastructure/Configuration/QdrantSkillsOptions.cs
    - src/QdrantSkillsMCP.Infrastructure/ServiceRegistration.cs
    - src/QdrantSkillsMCP.Infrastructure/Program.cs

key-decisions:
  - "QdrantClient constructed via QdrantGrpcClient(channel) for gRPC-Web/HTTP modes since high-level QdrantClient does not accept GrpcChannel directly"
  - "HTTP mode uses GrpcWebText encoding (not REST API) since Qdrant.Client is gRPC-only; REST would need a separate HTTP client"
  - "QdrantProtocol key auto-discovered by ConfigManager via reflection (no manual key list update needed)"

patterns-established:
  - "QdrantClientFactory pattern: factory creates protocol-appropriate QdrantClient from DI"
  - "Auto-detect protocol by host: localhost/127.0.0.1/::1/host.docker.internal -> gRPC, remote -> gRPC-Web"

requirements-completed: [TRANS-11, TRANS-12, TRANS-13]

# Metrics
duration: 5min
completed: 2026-03-31
---

# Phase 05 Plan 04: Qdrant Protocol Selection Summary

**Configurable Qdrant protocol (gRPC/gRPC-Web/HTTP) with auto-detection defaulting to gRPC-Web for remote hosts (Azure App Service compatible)**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-31T13:10:43Z
- **Completed:** 2026-03-31T13:15:27Z
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments
- QdrantClientFactory with protocol-aware client creation supporting gRPC, gRPC-Web, and HTTP modes
- Auto-detection: gRPC for localhost, gRPC-Web for remote hosts (Azure-compatible default)
- CLI flags --qdrant-grpc and --qdrant-http for explicit protocol override
- 15 unit tests covering protocol resolution, auto-detection, and client creation

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Grpc.Net.Client.Web package and QdrantProtocol config** - `fba931e` (feat)
2. **Task 2: Create QdrantClientFactory with protocol-aware client creation** - `0071040` (feat)
3. **Task 3: Add --qdrant-grpc/--qdrant-http flag parsing and unit tests** - `78aa365` (feat)

## Files Created/Modified
- `src/QdrantSkillsMCP.Infrastructure/Qdrant/QdrantClientFactory.cs` - Protocol-aware QdrantClient factory with gRPC, gRPC-Web, HTTP support
- `tests/QdrantSkillsMCP.UnitTests/Qdrant/QdrantProtocolTests.cs` - 15 unit tests for protocol resolution and client creation
- `src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj` - Added Grpc.Net.Client.Web package
- `src/QdrantSkillsMCP.Infrastructure/Configuration/QdrantSkillsOptions.cs` - Added QdrantProtocol property and QdrantProtocolType enum
- `src/QdrantSkillsMCP.Infrastructure/ServiceRegistration.cs` - Replaced inline QdrantClient with factory-based creation
- `src/QdrantSkillsMCP.Infrastructure/Program.cs` - Added --qdrant-grpc/--qdrant-http flag parsing

## Decisions Made
- QdrantClient constructed via QdrantGrpcClient(channel) for gRPC-Web/HTTP modes since high-level QdrantClient constructor does not accept GrpcChannel directly
- HTTP mode uses GrpcWebText encoding rather than a REST API, since Qdrant.Client is gRPC-only; a true REST client would require a separate HTTP implementation
- QdrantProtocol key is auto-discovered by ConfigManager via reflection on QdrantSkillsOptions (no manual key list update needed)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed QdrantClient constructor for gRPC-Web/HTTP modes**
- **Found during:** Task 2 (QdrantClientFactory creation)
- **Issue:** Plan used `new QdrantClient(channel)` but QdrantClient 1.17.0 does not accept GrpcChannel directly
- **Fix:** Used `new QdrantGrpcClient(channel)` then `new QdrantClient(grpcClient)` to construct via the low-level gRPC client
- **Files modified:** src/QdrantSkillsMCP.Infrastructure/Qdrant/QdrantClientFactory.cs
- **Verification:** dotnet build succeeds, all 264 tests pass
- **Committed in:** 0071040

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Essential fix for correct API usage. No scope creep.

## Issues Encountered
None beyond the constructor fix documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Qdrant protocol selection complete, ready for deployment to Azure App Service
- gRPC-Web auto-detection ensures zero-config Azure compatibility

---
*Phase: 05-http-transport*
*Completed: 2026-03-31*
