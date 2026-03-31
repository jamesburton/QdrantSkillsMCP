# Phase 5: HTTP Transport - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-03-31
**Phase:** 05-http-transport
**Areas discussed:** Transport flag design, Default port and URL binding, Health endpoint scope, Packaging risk strategy

---

## Transport Flag Design

### How should the transport flags interact?

| Option | Description | Selected |
|--------|-------------|----------|
| --sse and --streamable-http are separate modes | Three distinct modes: --stdio (default), --sse (legacy SSE only), --streamable-http (Streamable HTTP + auto-includes legacy SSE). --url implies --streamable-http. | |
| Single --http flag serves both transports | One HTTP mode: --http (or --url) starts Kestrel with MapMcp() serving both Streamable HTTP and legacy SSE from the same endpoint. | ✓ |

**User's choice:** Single --http flag serves both transports
**Notes:** Simpler UX — one flag, both protocols served.

### What happens if someone passes conflicting flags?

| Option | Description | Selected |
|--------|-------------|----------|
| Last flag wins | Simple precedence: rightmost transport flag wins. | |
| Error and exit | Print error message explaining conflict and exit with non-zero code. | ✓ |
| First flag wins | First transport flag wins, ignore subsequent ones. | |

**User's choice:** Error and exit
**Notes:** Fail fast on ambiguous input.

### How should --url work?

| Option | Description | Selected |
|--------|-------------|----------|
| --url sets both transport and listen address | e.g. --url http://0.0.0.0:8080 → implies HTTP mode, listen on that address. | ✓ |
| --url is listen address only, transport separate | --url sets listen address but still need --http to pick transport. | |

**User's choice:** --url sets both transport and listen address

---

## Default Port and URL Binding

### What default port should HTTP mode use?

| Option | Description | Selected |
|--------|-------------|----------|
| 8080 | Standard non-privileged HTTP port. Avoids 5000/5001 macOS AirPlay. | ✓ |
| 3001 | Common dev port, less conventional for .NET. | |
| Random available port | Pick any available port, print to stderr. | |

**User's choice:** 8080

### Should the listen URL be part of the layered config system?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — QDRANT_SKILLS_URL config key | Settable via env var, project config, user config, or --url flag. | ✓ |
| CLI and env only | --url flag and ASPNETCORE_URLS env var only. | |

**User's choice:** Yes — QDRANT_SKILLS_URL config key

---

## Health Endpoint Scope

### What should /health check?

| Option | Description | Selected |
|--------|-------------|----------|
| Liveness only | /health returns 200 OK if process running. | |
| Liveness + readiness | /health for liveness, /ready for readiness (Qdrant check). | |
| Combined health check | Single /health that checks Qdrant, 503 if down. | |

**User's choice:** Custom — /health quick liveness with "degraded" status if Qdrant connectivity fails, plus /health/json for full details.
**Notes:** User wants degraded (not 503) when Qdrant is unreachable. Server is still live. /health/json for ops detail.

### Should /health require authentication?

| Option | Description | Selected |
|--------|-------------|----------|
| No auth on /health | Health probes from container orchestrators can't present tokens. | ✓ |
| Auth required | Secure everything. Probes need token injection. | |

**User's choice:** No auth on /health

---

## Packaging Risk Strategy

### If PackAsTool + FrameworkReference fails, what's the fallback?

| Option | Description | Selected |
|--------|-------------|----------|
| NuGet = stdio only, Docker = HTTP | Accept NuGet tool stays stdio-only, HTTP is Docker/binary only. | |
| Separate QdrantSkillsMCP.Server project | New project for HTTP entry point, not packed as tool. | |
| Investigate first, decide if it breaks | Try it first, decide on fallback only if needed. | |

**User's choice:** Custom — Investigate first, but goal is keeping all modes in both distribution channels. Minimal fracturing preferred. Decide on fallback only if needed.
**Notes:** User strongly prefers single project with all modes. Avoid splitting unless forced.

### Should existing Dockerfile be updated or create new one?

| Option | Description | Selected |
|--------|-------------|----------|
| Update existing Dockerfile | Single Dockerfile with --http default entrypoint. | ✓ |
| Second Dockerfile for HTTP | Dockerfile.http alongside existing. | |

**User's choice:** Update existing Dockerfile

---

## Claude's Discretion

- CORS configuration details
- Kestrel KeepAliveTimeout tuning
- HTTP branch code structure in Program.cs
- ASP.NET Core health check implementation pattern

## Deferred Ideas

None
