---
phase: 3
slug: cli-distribution-and-bundled-skill
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-26
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 3.2.2 + NSubstitute 5.x |
| **Config file** | Tests already configured in UnitTests.csproj and IntegrationTests.csproj |
| **Quick run command** | `dotnet test tests/QdrantSkillsMCP.UnitTests --no-build -x` |
| **Full suite command** | `dotnet test --no-build` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/QdrantSkillsMCP.UnitTests --no-build -x`
- **After every plan wave:** Run `dotnet test --no-build`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 03-01-01 | 01 | 1 | CLI-01 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ConsoleHost" -x` | ❌ W0 | ⬜ pending |
| 03-01-02 | 01 | 1 | CLI-02 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ReplLoop" -x` | ❌ W0 | ⬜ pending |
| 03-02-01 | 02 | 1 | CLI-03 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ConfigWriter" -x` | ❌ W0 | ⬜ pending |
| 03-02-02 | 02 | 1 | CLI-04 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~AgentDetector" -x` | ❌ W0 | ⬜ pending |
| 03-02-03 | 02 | 1 | CLI-05 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~SnippetFallback" -x` | ❌ W0 | ⬜ pending |
| 03-02-04 | 02 | 1 | CLI-06 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ConfigWriter" -x` | ❌ W0 | ⬜ pending |
| 03-02-05 | 02 | 1 | CLI-07 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~SetupWizard" -x` | ❌ W0 | ⬜ pending |
| 03-03-01 | 03 | 2 | BSKL-01 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~SkillGuide" -x` | ❌ W0 | ⬜ pending |
| 03-03-02 | 03 | 2 | BSKL-02 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~FrequentSkills" -x` | ❌ W0 | ⬜ pending |
| 03-04-01 | 04 | 2 | DIST-01 | integration | `dotnet pack src/QdrantSkillsMCP.Infrastructure -c Release` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/QdrantSkillsMCP.UnitTests/Cli/ConsoleHostTests.cs` — stubs for CLI-01, CLI-02
- [ ] `tests/QdrantSkillsMCP.UnitTests/Cli/ReplLoopTests.cs` — stubs for CLI-02
- [ ] `tests/QdrantSkillsMCP.UnitTests/Setup/AgentDetectorTests.cs` — stubs for CLI-04
- [ ] `tests/QdrantSkillsMCP.UnitTests/Setup/ConfigWriterTests.cs` — stubs for CLI-03, CLI-05, CLI-06
- [ ] `tests/QdrantSkillsMCP.UnitTests/Setup/SetupWizardTests.cs` — stubs for CLI-07
- [ ] `tests/QdrantSkillsMCP.UnitTests/Skill/SkillGuideTests.cs` — stubs for BSKL-01
- [ ] `tests/QdrantSkillsMCP.UnitTests/Skill/FrequentSkillsTests.cs` — stubs for BSKL-02

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| NuGet tool install + run via `dnx` | DIST-01 | Requires global tool install in clean environment | `dotnet pack`, install, run `dnx qdrant-skills-mcp --console status` |
| REPL interactive session | CLI-02 | Interactive terminal I/O cannot be automated in xunit | Start `--console`, type commands, verify output, exit with `quit` |
| Setup writes real agent config | CLI-03 | Requires actual agent config files on disk | Run `--setup --agent claude --level user` on machine with Claude installed |

*All other behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
