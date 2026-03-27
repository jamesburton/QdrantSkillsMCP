---
phase: 04
slug: configuration-management
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-27
---

# Phase 04 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (3.2.2) with MTP |
| **Config file** | tests/QdrantSkillsMCP.UnitTests/QdrantSkillsMCP.UnitTests.csproj |
| **Quick run command** | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~Config" -x` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~5 seconds (unit), ~20 seconds (full) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~Config" -x`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 04-01-01 | 01 | 1 | CFG-02 | unit | `dotnet test --filter "FullyQualifiedName~ConfigManager" -x` | ❌ W0 | ⬜ pending |
| 04-01-02 | 01 | 1 | CFG-03 | unit | `dotnet test --filter "FullyQualifiedName~ConfigManager" -x` | ❌ W0 | ⬜ pending |
| 04-01-03 | 01 | 1 | CFG-04 | unit | `dotnet test --filter "FullyQualifiedName~ConfigManager" -x` | ❌ W0 | ⬜ pending |
| 04-01-04 | 01 | 1 | CFG-06 | unit | `dotnet test --filter "FullyQualifiedName~ConfigManager" -x` | ❌ W0 | ⬜ pending |
| 04-01-05 | 01 | 1 | CFG-07 | unit | `dotnet test --filter "FullyQualifiedName~ConfigManager" -x` | ❌ W0 | ⬜ pending |
| 04-01-06 | 01 | 1 | CFG-09 | unit | `dotnet test --filter "FullyQualifiedName~ConfigProfile" -x` | ❌ W0 | ⬜ pending |
| 04-01-07 | 01 | 1 | CFG-10 | unit | `dotnet test --filter "FullyQualifiedName~ShellDetect" -x` | ❌ W0 | ⬜ pending |
| 04-01-08 | 01 | 1 | CFG-11 | unit | `dotnet test --filter "FullyQualifiedName~SecretMask" -x` | ❌ W0 | ⬜ pending |
| 04-02-01 | 02 | 2 | CFG-01 | unit | `dotnet test --filter "FullyQualifiedName~ConfigCommand" -x` | ❌ W0 | ⬜ pending |
| 04-02-02 | 02 | 2 | CFG-05 | integration | `dotnet test --filter "FullyQualifiedName~ConfigValidate" -x` | ❌ W0 | ⬜ pending |
| 04-02-03 | 02 | 2 | CFG-08 | manual-only | N/A | N/A | ⬜ pending |
| 04-02-04 | 02 | 2 | CFG-12 | unit | `dotnet test --filter "FullyQualifiedName~UserConfig" -x` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/QdrantSkillsMCP.UnitTests/Config/ConfigManagerTests.cs` — stubs for CFG-02 through CFG-07, CFG-09
- [ ] `tests/QdrantSkillsMCP.UnitTests/Config/ShellDetectorTests.cs` — stubs for CFG-10
- [ ] `tests/QdrantSkillsMCP.UnitTests/Config/SecretMaskTests.cs` — stubs for CFG-11
- [ ] `tests/QdrantSkillsMCP.UnitTests/Config/ConfigCommandTests.cs` — stubs for CFG-01
- [ ] ConfigManager tests should use temp directories (constructor-injected paths like FrequentSkillsService)

*Existing infrastructure covers xUnit v3 framework and Aspire test host.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Interactive wizard prompts | CFG-08 | Requires real terminal (Console.ReadKey) | Run `--config` with no args, verify Spectre.Console prompts appear |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
