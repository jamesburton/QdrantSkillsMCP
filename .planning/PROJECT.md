# QdrantSkillsMCP

## What This Is

A .NET 10 C# MCP (Model Context Protocol) server that provides vector-based skill storage and retrieval using Qdrant. AI agents (Claude Code, Copilot, Codex, Cursor, Windsurf, Zed, etc.) can search, load, add, update, archive, and delete Claude Code skills (markdown with YAML frontmatter) via MCP tools. Skills are embedded and stored in Qdrant for semantic vector search, enabling agents to find relevant skills based on context and prompt content. Ships as a NuGet tool (`dnx QdrantSkillsMCP`) with a bundled self-teaching SKILL.md and a multi-agent setup wizard.

## Core Value

Agents can semantically search and retrieve the right skills at the right time — turning a flat collection of skill files into an intelligent, context-aware skill library accessible to any MCP-compatible agent.

## Requirements

### Validated

- ✓ MCP server runs via `--stdio` for standard MCP transport — v1.0
- ✓ Connects to configurable Qdrant instance (default: localhost:6334, `skills` collection) — v1.0
- ✓ `search-skills` tool: vector-based semantic search with configurable temperature, max-results, context summary + prompt input — v1.0
- ✓ `load-skill` tool: fetch specific skill(s) by name, supports reloading updated skills — v1.0
- ✓ `add-skill` / `update-skill` tools: persist skills (markdown with YAML frontmatter) to Qdrant with vector embeddings — v1.0
- ✓ `archive-skill` tool: soft-hide obsolete skills without deletion — v1.0
- ✓ `delete-skill` tool: permanently remove skills from the collection — v1.0
- ✓ Configurable embedding provider (OpenAI, ONNX, Ollama, Azure OpenAI) — v1.0
- ✓ `--console` parameter: single-shot CLI subcommands with JSON output, or REPL if no subcommand given — v1.0
- ✓ `--setup` command: auto-configures MCP server entry in agent config files; supports 9+ agents; auto-writes or falls back to snippets; interactive or non-interactive — v1.0
- ✓ `--names` option: return skill names only — v1.0
- ✓ `--summaries` option: return name + short summary — v1.0
- ✓ Session tracking: tracks which skills have been returned per session; includes `ALREADY LOADED SKILLS: {list}` — v1.0
- ✓ Session identification: defaults to MCP connection lifecycle, supports explicit session ID override — v1.0
- ✓ Packaged as NuGet tool, invoked via `dnx QdrantSkillsMCP` — v1.0
- ✓ Bundled SKILL.md: teaches agents how to use QdrantSkillsMCP effectively + curated short-list of frequent skills — v1.0
- ✓ Local development via Aspire v13.2 AppHost running Qdrant — v1.0
- ✓ Full XUnit v3 (MTP) test coverage using Aspire testing framework — v1.0
- ✓ Layered configuration (env > project > user > default), `--config` CLI with 8 subcommands, secret masking, TLS auto-detection — v1.0

### Active

- [ ] Authentication: API key (bearer token) for simple cases, OAuth/OIDC for enterprise
- [ ] skills-guru integration: full integration as a first-class backend — push/sync TO and query/search FROM QdrantSkillsMCP

### Out of Scope

- GUI / web dashboard — CLI and MCP tools only
- Skill authoring/editing UI — skills are authored as markdown files externally
- Multi-tenant SaaS hosting — local/self-hosted tool
- Real-time collaborative editing of skills
- Non-.NET client SDKs — agents interact via MCP protocol

## Context

**Shipped v1.0** (2026-03-30): 10,445 C# LOC across 5 projects, 106 commits, 5 days of execution.

**Tech stack:** .NET 10, C#, ModelContextProtocol C# SDK, Qdrant.Client, Microsoft.Extensions.AI, Aspire 13.2, XUnit v3 (MTP), YamlDotNet, Spectre.Console, OllamaSharp, Semantic Kernel (ONNX bridge), Tomlyn (TOML for Codex)

**Agents supported by setup wizard:** Claude Code, GitHub Copilot (VS Code), Codex (CLI), opencode, Docker MCP Toolkit, KiloCode, Factory Droid, Cursor, Windsurf, Zed

**Embedding providers:** OpenAI (text-embedding-3-small/large), ONNX (all-MiniLM-L6-v2, 384-dim), Ollama (any model), Azure OpenAI

**Known issues / tech debt:**
- REQUIREMENTS.md did not include CFG-01..CFG-12 (Phase 4 config requirements) — tracked in ROADMAP only
- STATE.md showed Phase 4 as 1/2 complete despite 04-02-SUMMARY.md existing (stale state)
- Integration tests for --config validate require a live Qdrant instance (not mocked)

## Constraints

- **Runtime**: .NET 10 — required for latest Aspire and Agent Framework compatibility
- **Test Framework**: XUnit v3 with MTP (Microsoft Testing Platform) runner
- **Aspire Version**: v13.2 for AppHost and testing infrastructure
- **Package Distribution**: NuGet tool package, invoked via `dnx QdrantSkillsMCP`
- **MCP Transport**: stdio as primary transport (standard for local MCP servers)
- **Skill Schema**: Must preserve full Claude Code skill format (frontmatter + markdown body) — no lossy transformations

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| ModelContextProtocol C# SDK for MCP server | Official SDK, maintained by MCP spec authors | ✓ Good — stable, well-integrated |
| Qdrant as vector store | Open-source, Aspire integration, proven .NET client | ✓ Good — Aspire fixture worked reliably |
| Configurable embedding providers via IEmbeddingGenerator | Users have different constraints (offline, cost, latency) | ✓ Good — clean provider swap with PostConfigure |
| dnx tool packaging | Standard .NET tool distribution | ✓ Good — installs and runs as expected |
| Session tracking per MCP connection with __default__ sentinel | Natural session boundary with override support | ✓ Good — ConcurrentDictionary nesting clean |
| API key + OAuth deferred to v2 | Complexity tradeoff for MVP | — Pending (v2) |
| skills-guru integration deferred to v2 | Scope containment for MVP | — Pending (v2) |
| Aspire.AppHost.Sdk via NuGet instead of workload | Workload deprecated in .NET 10 | ✓ Good — avoids NETSDK1228 error |
| .slnx solution format | New default in .NET 10 SDK | ✓ Good — toolchain support fine |
| Infrastructure project as MCP entry point (OutputType=Exe) | Core stays dependency-free | ✓ Good — clean architecture maintained |
| FakeEmbeddingService with SHA-256 hash chaining | Deterministic vectors for parallel-safe tests | ✓ Good — per-class unique collections |
| JsonConfigWriterBase for agent config writers | Abstracts backup/merge/validate pattern | ✓ Good — consistent across all agents |
| Copilot uses 'servers' root key (not 'mcpServers') | VS Code MCP spec difference | ✓ Good — verified against spec |
| Codex uses Tomlyn for TOML read-modify-write | Avoids hand-rolled TOML parser | ✓ Good — reliable round-trip |
| AddSetupServices separate from AddQdrantSkillsInfrastructure | Setup mode must not connect to Qdrant | ✓ Good — clean DI separation |
| ConfigManager uses reflection for configurable keys | Avoids manual key enum maintenance | ✓ Good — automatically tracks new options |
| Source precedence via annotation tracking | Shows users where each value came from | ✓ Good — UX clarity |

---
*Last updated: 2026-03-30 after v1.0 milestone*
