---
phase: 2
slug: search-intelligence-and-embedding-providers
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-25
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 3.2.2 + Aspire.Hosting.Testing 13.2.0 |
| **Config file** | Tests use Aspire AppHost builder (no separate config file) |
| **Quick run command** | `dotnet test tests/QdrantSkillsMCP.UnitTests -x` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/QdrantSkillsMCP.UnitTests -x`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 02-01-01 | 01 | 1 | SRCH-07 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~OutputMode" -x` | ❌ W0 | ⬜ pending |
| 02-01-02 | 01 | 1 | SRCH-08 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~OutputMode" -x` | ❌ W0 | ⬜ pending |
| 02-01-03 | 01 | 1 | SRCH-09 | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "FullyQualifiedName~Session" -x` | Partial | ⬜ pending |
| 02-01-04 | 01 | 1 | SRCH-10 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~SessionTracker" -x` | Partial | ⬜ pending |
| 02-02-01 | 02 | 2 | EMB-03 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~Onnx" -x` | ❌ W0 | ⬜ pending |
| 02-02-02 | 02 | 2 | EMB-04 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~Ollama" -x` | ❌ W0 | ⬜ pending |
| 02-02-03 | 02 | 2 | EMB-05 | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~AzureOpenAi" -x` | ❌ W0 | ⬜ pending |
| 02-02-04 | 02 | 2 | EMB-06 | unit+integration | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~DimensionValidator" -x` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/QdrantSkillsMCP.UnitTests/Tools/OutputModeTests.cs` — stubs for SRCH-07, SRCH-08
- [ ] `tests/QdrantSkillsMCP.UnitTests/Session/KeyedSessionTrackerTests.cs` — stubs for SRCH-10
- [ ] `tests/QdrantSkillsMCP.UnitTests/Embedding/OnnxEmbeddingServiceTests.cs` — stubs for EMB-03
- [ ] `tests/QdrantSkillsMCP.UnitTests/Embedding/OllamaEmbeddingServiceTests.cs` — stubs for EMB-04
- [ ] `tests/QdrantSkillsMCP.UnitTests/Embedding/AzureOpenAiEmbeddingServiceTests.cs` — stubs for EMB-05
- [ ] `tests/QdrantSkillsMCP.UnitTests/Qdrant/DimensionValidatorTests.cs` — stubs for EMB-06
- [ ] `tests/QdrantSkillsMCP.IntegrationTests/SessionIdIntegrationTests.cs` — stubs for SRCH-09, SRCH-10

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Ollama with real container | EMB-04 | Requires Docker + Ollama model download | 1. Start Ollama container 2. Pull embedding model 3. Set EmbeddingProvider=Ollama 4. Verify search works |
| ONNX auto-download from HuggingFace | EMB-03 | Requires network + HF access | 1. Remove local model file 2. Set EmbeddingProvider=LocalONNX 3. Start server 4. Verify model downloads and search works |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
