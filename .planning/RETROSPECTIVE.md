# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

## Milestone: v1.0 — MVP

**Shipped:** 2026-03-30
**Phases:** 4 | **Plans:** 14
**Timeline:** 5 days (2026-03-25 → 2026-03-30)

### What Was Built
- MCP stdio server with 7 skill tools (search, load, add, update, archive, delete, list) backed by Qdrant vector storage and lossless YAML frontmatter round-tripping
- Full XUnit v3 (MTP) test suite with Aspire integration tests, deterministic fake embeddings, and parallel-safe collection isolation per test class
- 4 pluggable embedding providers (OpenAI, ONNX via Semantic Kernel bridge, Ollama, Azure OpenAI) with startup dimension mismatch validation
- Session-aware search: tracks already-loaded skills per connection; output modes (full/names/summaries) for progressive disclosure
- Console CLI (single-shot subcommands + interactive REPL with Spectre.Console) and multi-agent setup wizard auto-writing configs for 9+ agents
- Layered configuration system (env > project > user > default) with `--config` CLI, TLS auto-detection, secret masking, and interactive wizard

### What Worked
- **Coarse granularity planning**: 2-5 plans per phase kept phases focused without over-specification
- **Aspire testing infrastructure**: Built in Phase 1, paid dividends throughout — Qdrant fixture reused in every integration test phase
- **JsonConfigWriterBase pattern**: Abstracting backup/merge/validate for agent config writers meant adding new agents (Cursor, Windsurf, Zed) was trivial
- **SHA-256 hash-based fake embeddings**: Deterministic, parallel-safe, required zero Qdrant state cleanup between tests
- **Gap-closure plan (03-04)**: Detected DI wiring gap before integration testing — saved debugging time
- **yolo mode**: Planning + execution without confirmation gates saved significant friction on a well-scoped project

### What Was Inefficient
- **REQUIREMENTS.md not updated for Phase 4**: CFG-01..CFG-12 requirements were defined in ROADMAP.md but never added to REQUIREMENTS.md — created traceability gap at milestone close
- **STATE.md staleness**: After 04-02 completed, STATE.md showed 13/14 plans (never updated) — required manual correction at archive time
- **ONNX bridging complexity**: BertOnnxTextEmbeddingGenerationService required Semantic Kernel bridge to Microsoft.Extensions.AI — non-obvious, required research

### Patterns Established
- **Separate DI registration for modes**: `AddQdrantSkillsInfrastructure` vs `AddSetupServices` prevents Qdrant connections in setup-only mode
- **Per-test-class unique Qdrant collection names**: Eliminates all cross-test state leakage without cleanup logic
- **PostConfigure for dimension override**: Ensures ONNX dimension (384) overrides OpenAI default (1536) even if set before provider selection
- **ConfigManager via reflection**: Derives configurable keys from QdrantSkillsOptions automatically — no manual key enum maintenance

### Key Lessons
1. **Track all requirements in REQUIREMENTS.md when adding phases mid-milestone** — ROADMAP-only requirements create closure gaps
2. **Update STATE.md after every plan, not just check** — stale progress counts compound and require manual correction at milestone close
3. **Research ONNX + M.E.AI bridging early** — the SK bridge is non-obvious and underdocumented; plan 02-02 would have benefited from earlier research
4. **Agent config file format verification is essential** — Copilot `servers` vs `mcpServers` difference caught during execution; pre-research recommended

### Cost Observations
- Sessions: ~8 sessions over 5 days
- Notable: 14 plans at ~7min average = extremely high execution velocity for the scope delivered

---

## Cross-Milestone Trends

### Process Evolution

| Milestone | Phases | Plans | Avg/Plan | Key Change |
|-----------|--------|-------|----------|------------|
| v1.0 | 4 | 14 | 7min | First milestone — baseline established |

### Cumulative Quality

| Milestone | Approx Tests | Notes |
|-----------|-------------|-------|
| v1.0 | 200+ | Unit + integration, Aspire-backed |
