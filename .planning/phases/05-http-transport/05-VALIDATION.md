---
phase: 5
slug: http-transport
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-31
---

# Phase 5 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | XUnit v3 (MTP) + Aspire testing |
| **Config file** | `tests/QdrantSkillsMCP.UnitTests/QdrantSkillsMCP.UnitTests.csproj` |
| **Quick run command** | `dotnet test tests/QdrantSkillsMCP.UnitTests/ --no-build` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/QdrantSkillsMCP.UnitTests/ --no-build`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| TBD | TBD | TBD | TRANS-01 | integration | `dotnet test --filter "HttpTransport"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | TRANS-02 | integration | `dotnet test --filter "LegacySse"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | TRANS-03 | unit | `dotnet test --filter "StdioRegression"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | TRANS-04 | integration | `dotnet test --filter "Health"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | TRANS-08 | manual | `dotnet pack` | N/A | ⬜ pending |
| TBD | TBD | TBD | TRANS-09 | integration | `dotnet test` (existing suite) | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] HTTP transport test infrastructure (WebApplicationFactory or in-process test host)
- [ ] Health check test helpers
- [ ] Stdio regression baseline (verify existing tests still pass after package upgrades)

*Planner will refine these into concrete task specs.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| PackAsTool + FrameworkReference produces valid NuGet package | TRANS-08 | Packaging validation requires dotnet pack + dotnet tool install round-trip | Run `dotnet pack`, install globally, run `qdrant-skills-mcp --stdio`, verify MCP handshake |
| Dockerfile builds and runs with --http | TRANS-10 | Docker build requires Docker daemon | `docker build -t test . && docker run --rm -p 8080:8080 test --http` |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
