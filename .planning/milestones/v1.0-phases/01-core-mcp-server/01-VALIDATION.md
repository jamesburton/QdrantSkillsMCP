---
phase: 1
slug: core-mcp-server
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-25
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 3.2.0+ with MTP |
| **Config file** | none — Wave 0 installs |
| **Quick run command** | `dotnet test tests/QdrantSkillsMCP.UnitTests --no-build` |
| **Full suite command** | `dotnet test --no-build` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/QdrantSkillsMCP.UnitTests --no-build`
- **After every plan wave:** Run `dotnet test --no-build`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 01-01-01 | 01 | 0 | DIST-02 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "AspireQdrant" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-01-02 | 01 | 0 | DIST-03 | smoke | `dotnet test --no-build` | ❌ W0 | ⬜ pending |
| 01-02-01 | 02 | 1 | QDR-04 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "CollectionAutoCreate" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-02-02 | 02 | 1 | CRUD-05 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "YamlRoundTrip" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-02-03 | 02 | 1 | EMB-01 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "EmbeddingAbstraction" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-03-01 | 03 | 2 | CRUD-01 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "AddSkill" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-03-02 | 03 | 2 | CRUD-02 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "UpdateSkill" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-03-03 | 03 | 2 | CRUD-03 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "DeleteSkill" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-03-04 | 03 | 2 | CRUD-04 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "ArchiveSkill" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-03-05 | 03 | 2 | SRCH-01 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "SemanticSearch" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-03-06 | 03 | 2 | SRCH-02 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "SearchTemperature" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-03-07 | 03 | 2 | SRCH-03 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "SearchMaxResults" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-03-08 | 03 | 2 | SRCH-04 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "LoadSkill" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-03-09 | 03 | 2 | SRCH-05 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "LoadSkillReload" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-03-10 | 03 | 2 | SRCH-06 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "ListSkills" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-04-01 | 04 | 3 | MCP-01 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "ServerStartup" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-04-02 | 04 | 3 | MCP-02 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "ToolDiscovery" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-04-03 | 04 | 3 | QDR-01 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "QdrantConnection" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-04-04 | 04 | 3 | QDR-02 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "CollectionName" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-04-05 | 04 | 3 | QDR-03 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "ApiKeyConfig" --no-build -x` | ❌ W0 | ⬜ pending |
| 01-04-06 | 04 | 3 | EMB-02 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "OpenAiEmbedding" --no-build -x` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QdrantSkillsMCP.sln` — solution file
- [ ] `src/QdrantSkillsMCP.Core/QdrantSkillsMCP.Core.csproj` — interfaces and models, zero dependencies
- [ ] `src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj` — NuGet refs (MCP SDK, Qdrant, M.E.AI, YamlDotNet)
- [ ] `src/QdrantSkillsMCP.AppHost/QdrantSkillsMCP.AppHost.csproj` — Aspire AppHost
- [ ] `tests/QdrantSkillsMCP.UnitTests/QdrantSkillsMCP.UnitTests.csproj` — xunit.v3 + MTP config
- [ ] `tests/QdrantSkillsMCP.IntegrationTests/QdrantSkillsMCP.IntegrationTests.csproj` — Aspire.Hosting.Testing + xunit.v3
- [ ] Test stubs for all requirements listed in verification map

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| MCP stdio transport end-to-end with real client | MCP-01 | Requires actual MCP client process | 1. Build server 2. Connect with MCP client via stdio 3. Verify tool listing |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
