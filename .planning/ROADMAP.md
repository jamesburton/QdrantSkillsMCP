# Roadmap: QdrantSkillsMCP

## Overview

QdrantSkillsMCP goes from zero to a fully packaged MCP server in three phases. Phase 1 builds the complete core: MCP stdio transport, Qdrant persistence, all CRUD and search tools, lossless skill round-tripping, and the first embedding provider (OpenAI), all backed by Aspire dev infrastructure and XUnit v3 tests. Phase 2 adds search intelligence (session tracking, output modes) and embedding provider flexibility (ONNX, Ollama, Azure OpenAI, dimension validation). Phase 3 delivers the developer experience layer: console CLI/REPL, multi-agent setup wizard, bundled self-teaching skill, and NuGet tool packaging for distribution.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Core MCP Server** - Working MCP server with Qdrant storage, full skill CRUD, semantic search, and test infrastructure
- [x] **Phase 2: Search Intelligence and Embedding Providers** - Session tracking, output modes, and pluggable embedding providers with dimension safety
- [ ] **Phase 3: CLI, Distribution, and Bundled Skill** - Console mode, multi-agent setup wizard, bundled SKILL.md, and NuGet tool packaging

## Phase Details

### Phase 1: Core MCP Server
**Goal**: Agents can connect via MCP stdio, store skills in Qdrant, and search/retrieve them semantically
**Depends on**: Nothing (first phase)
**Requirements**: MCP-01, MCP-02, QDR-01, QDR-02, QDR-03, QDR-04, CRUD-01, CRUD-02, CRUD-03, CRUD-04, CRUD-05, SRCH-01, SRCH-02, SRCH-03, SRCH-04, SRCH-05, SRCH-06, EMB-01, EMB-02, DIST-02, DIST-03
**Success Criteria** (what must be TRUE):
  1. An MCP client can connect via stdio and discover all skill tools (search, load, add, update, delete, archive, list)
  2. A skill (markdown with YAML frontmatter) can be added, retrieved by name, updated, and deleted with zero data loss in the round-trip
  3. Semantic search returns relevant skills ranked by vector similarity, with configurable temperature and max-results
  4. Aspire AppHost starts Qdrant automatically and integration tests pass end-to-end against it
  5. The Qdrant collection is auto-created with correct vector dimensions and payload indexes on first use
**Plans**: 5 plans

Plans:
- [x] 01-01-PLAN.md -- Solution scaffold, Core interfaces/models, AppHost, configuration
- [x] 01-02-PLAN.md -- Infrastructure services (YAML parser, Qdrant repo, embeddings, session tracker, DI wiring)
- [x] 01-03-PLAN.md -- MCP tool classes (CRUD + search) and Program.cs entry point
- [x] 01-04-PLAN.md -- Unit tests (parser, validator, session tracker, embedding service)
- [x] 01-05-PLAN.md -- Integration tests (Aspire fixture, CRUD, search, collection init)

### Phase 2: Search Intelligence and Embedding Providers
**Goal**: Users can choose their embedding provider and get session-aware, progressively-disclosed search results
**Depends on**: Phase 1
**Requirements**: SRCH-07, SRCH-08, SRCH-09, SRCH-10, EMB-03, EMB-04, EMB-05, EMB-06
**Success Criteria** (what must be TRUE):
  1. Search results include "ALREADY LOADED SKILLS" listing skills previously returned in the current session
  2. Users can switch between OpenAI, ONNX, Ollama, and Azure OpenAI embedding providers via configuration
  3. Startup detects and reports embedding dimension mismatches when the configured provider differs from the collection's existing vectors
  4. The `--names` and `--summaries` output modes return progressively less data for large skill libraries
**Plans**: 3 plans

Plans:
- [x] 02-01-PLAN.md -- Search intelligence: output modes, keyed session tracking, sessionId param, reset-session tool
- [x] 02-02-PLAN.md -- Embedding providers: ONNX, Ollama, Azure OpenAI services + provider selection + AppHost
- [x] 02-03-PLAN.md -- Dimension validation IHostedService + integration tests for Phase 2

### Phase 3: CLI, Distribution, and Bundled Skill
**Goal**: Users can install via dnx, configure any supported agent in one command, and agents learn to use the server from its bundled skill
**Depends on**: Phase 2
**Requirements**: CLI-01, CLI-02, CLI-03, CLI-04, CLI-05, CLI-06, CLI-07, DIST-01, BSKL-01, BSKL-02
**Success Criteria** (what must be TRUE):
  1. `dnx QdrantSkillsMCP --console search "authentication"` returns JSON results; `--console` without a subcommand enters an interactive REPL
  2. `dnx QdrantSkillsMCP --setup` detects installed agents (Claude, Copilot, Codex, etc.) and writes correct MCP config entries, with backup and fallback to manual snippets
  3. The bundled SKILL.md teaches an agent how to use QdrantSkillsMCP and includes a curated short-list of frequently used skills
  4. The NuGet tool package installs and runs correctly via `dnx QdrantSkillsMCP`
**Plans**: TBD

Plans:
- [ ] 03-01: TBD
- [ ] 03-02: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Core MCP Server | 5/5 | Complete | 2026-03-25 |
| 2. Search Intelligence and Embedding Providers | 3/3 | Complete | 2026-03-25 |
| 3. CLI, Distribution, and Bundled Skill | 0/2 | Not started | - |
