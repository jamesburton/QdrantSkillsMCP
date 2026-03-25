# Project Research Summary

**Project:** QdrantSkillsMCP
**Domain:** .NET MCP Server with Qdrant Vector Storage for Skill Management
**Researched:** 2026-03-25
**Confidence:** HIGH

## Executive Summary

QdrantSkillsMCP is a .NET 10 MCP server that gives AI agents semantic search and full CRUD lifecycle management over a library of markdown-based skills stored in Qdrant. The product occupies a unique niche: no existing tool combines vector search, full write operations, session intelligence, and multi-agent auto-configuration in a single server. The nearest competitors (Qdrant Official MCP, K-Dense claude-skills-mcp, skill-mcp) are either read-only, single-provider, or lack session tracking entirely. The recommended build approach is a clean layered .NET solution — Core (domain contracts), Infrastructure (Qdrant + embedding providers), executable (MCP tools + CLI) — orchestrated locally with Aspire 13.x, packaged as a `dnx`-installable NuGet tool.

The stack is well-understood and all dependencies are GA and production-grade. The official ModelContextProtocol C# SDK (v1.1.0) provides the stdio transport and DI-friendly tool registration pattern. Qdrant's official .NET client (v1.17.0) handles gRPC persistence. Microsoft.Extensions.AI (v10.4.x) provides the embedding abstraction that makes provider-swapping transparent. The architecture follows well-established .NET patterns: strategy pattern for pluggable embeddings, constructor-injected MCP tool classes, dual-mode entry point (stdio vs console), and Qdrant as both the vector index and document store.

The three non-negotiable risks are: (1) stdout pollution silently breaking the MCP stdio transport — logging must be stderr-only from line one; (2) embedding dimension mismatch corrupting the Qdrant collection when users switch providers — requires startup validation and a `--reindex` migration path; (3) lossy YAML frontmatter transformation violating the round-trip fidelity constraint — solved by storing raw content verbatim in the Qdrant payload alongside parsed fields. All three must be designed into Phase 1 before any feature work begins.

## Key Findings

### Recommended Stack

The stack is mature, fully GA, and purpose-built for this use case. .NET 10 (LTS, GA November 2025) is the correct runtime — it enables the `dnx` tool runner for one-command installation, C# 14 features, and Aspire 13.x compatibility. The project's original "Aspire v9.2" constraint is outdated: Aspire jumped to v13 in November 2025, and the 9.x line is end-of-support. All Aspire packages must be pinned to 13.x.

**Core technologies:**
- **.NET 10 (LTS):** Runtime and SDK — GA, 3-year support window, enables dnx and Aspire 13.x
- **ModelContextProtocol 1.1.0:** Official C# MCP SDK (Microsoft + Anthropic, 5.2M downloads) — provides `AddMcpServer()`, `WithStdioServerTransport()`, `WithToolsFromAssembly()`
- **Qdrant.Client 1.17.0:** Official gRPC client — required by Aspire integration, broadest .NET compatibility
- **Aspire 13.x:** Local dev orchestration — first-party Qdrant hosting, `DistributedApplicationTestingBuilder` for integration tests, zero manual Docker setup
- **Microsoft.Extensions.AI 10.4.x:** GA embedding abstraction — `IEmbeddingGenerator<string, Embedding<float>>` enables clean provider swap with no core logic changes
- **OllamaSharp 5.4.16:** Local Ollama embeddings — Microsoft deprecated their own Ollama package in favor of this
- **YamlDotNet 16.3.0:** YAML frontmatter parsing — only library needed when full Markdown AST is not required
- **xUnit v3 3.2.2:** Test framework — best Microsoft Testing Platform integration, community standard

**Critical version warnings:**
- Do NOT use Aspire 9.2 packages — end of support, replaced by Aspire 13.x
- Do NOT use `Microsoft.Extensions.AI.Ollama` — officially deprecated, replaced by OllamaSharp
- Do NOT use `ModelContextProtocol.AspNetCore` — that is for HTTP/SSE transport; this project uses stdio

### Expected Features

The competitive landscape is clear. No existing tool combines full CRUD, semantic search, session tracking, and multi-agent setup in one package. QdrantSkillsMCP's differentiation is strongest in session intelligence and the multi-agent `--setup` wizard.

**Must have (table stakes):**
- Semantic skill search — core value proposition, every competitor has this
- Full CRUD (add/update/delete/list) — K-Dense and skill-mcp are read-only, a critical gap to fill
- Skill retrieval by name (`load-skill`) — direct lookup for known skills
- Configurable embedding provider — offline, cost, and latency constraints vary by user
- stdio MCP transport — required by MCP spec for local server integration
- YAML frontmatter round-trip preservation — non-negotiable per project constraints
- Qdrant connection configuration — URL, API key, collection name

**Should have (competitive differentiators):**
- Session tracking ("already loaded" awareness) — unique; prevents redundant context stuffing
- Archive (soft-delete) — competitors have hard delete only
- `--names` and `--summaries` output modes — progressive disclosure for large skill libraries
- Bundled SKILL.md (self-teaching meta-skill) — reduces cold-start friction
- `--console` mode (CLI + REPL) — enables non-MCP scripting and manual management
- Additional local embedding providers (ONNX, Ollama) — supports offline use

**Phase 3 (ecosystem):**
- `--setup` auto-configuration for 7+ agents (Claude, Copilot, Codex, OpenCode, Docker Agent, Kilocode, Factory Droid)
- skills-guru integration (push/sync)
- NuGet tool packaging with `dnx` distribution

**Defer to v2+ / indefinitely:**
- OAuth/OIDC authentication (API key is sufficient initially)
- GUI/web dashboard, skill authoring UI, marketplace, knowledge graphs, hybrid BM25+vector search

### Architecture Approach

The recommended architecture is a four-project .NET solution with clean layering: Core (zero-dependency domain contracts), Infrastructure (concrete implementations of all external dependencies), executable (MCP tools + CLI entry points), and AppHost (Aspire dev orchestration only, not shipped). This structure enables Core to be built first, establishing all interfaces before any infrastructure work begins, which in turn allows parallel development of embedding providers.

**Major components:**
1. **MCP Tool Layer** — one class per tool (`[McpServerToolType]` + constructor injection), registered via `WithToolsFromAssembly()`
2. **SkillService** — core business logic coordinating search, CRUD, session tracking, and embedding
3. **ISkillRepository / QdrantSkillRepository** — Qdrant abstraction; stores full raw content in payload alongside structured fields
4. **IEmbeddingProvider** — pluggable strategy (ONNX / OpenAI / Azure OpenAI / Ollama); selected via config at startup
5. **SessionTracker** — in-memory per-connection tracking of returned skills; TTL-based expiry
6. **SkillParser** — YAML frontmatter extraction preserving raw original content for lossless round-trips
7. **Dual-mode entry point** — `--setup` wizard, `--console` CLI/REPL, or default stdio MCP transport

**Key data flow:** Query → embed query → Qdrant vector search → session-aware filtering → JSON response. Ingestion: raw markdown → parse YAML → extract embeddable text → embed → upsert to Qdrant (raw content + structured payload fields).

### Critical Pitfalls

1. **stdout pollution breaks stdio transport** — Any `Console.WriteLine` or console-sink logger corrupts the MCP JSON-RPC stream. Configure stderr-only logging before writing any other code. Add an integration test asserting stdout contains only valid JSON-RPC.

2. **Embedding dimension mismatch corrupts the collection** — Switching providers (e.g., 384-dim ONNX to 1536-dim OpenAI) makes existing data unsearchable. Store model identifier and dimension in collection metadata; validate on startup; provide `--reindex` migration command.

3. **Lossy YAML transformation violates round-trip fidelity** — Parsing frontmatter into structured fields and reconstructing on read drops comments, reorders keys, alters whitespace. Store the complete original markdown as `raw_content` in the Qdrant payload and return that verbatim.

4. **Leaky embedding abstraction** — A naive `EmbedAsync(string)` interface fails to handle batching, rate limits, and token limits that differ per provider. Design the interface with batch operations and a capabilities contract (`Dimensions`, `MaxTokens`) from the start.

5. **Qdrant collection not initialized correctly** — Missing payload indexes on `name`, `tags`, and `archived` fields cause slow filtered searches. Create payload indexes before inserting data and implement a startup health check (Aspire's Qdrant integration historically lacks built-in health checks — GitHub issue #5768).

## Implications for Roadmap

Based on the dependency graph from FEATURES.md and the build order from ARCHITECTURE.md, a four-phase structure is strongly recommended. Phases map directly to the feature research MVP breakdown.

### Phase 1: Foundation and Core MCP Server

**Rationale:** All features depend on Qdrant connectivity and MCP transport. Critical pitfalls #1, #2, and #3 must be addressed before any feature work or they will corrupt everything built on top. This phase establishes the interfaces that allow parallel work in Phase 2.

**Delivers:** Working MCP server with stdio transport, Qdrant connection, collection initialization with payload indexes, YAML round-trip parser, and the Core project interfaces. Basic `search-skills`, `load-skill`, `add-skill`, `update-skill`, `delete-skill`, and `list-skills` tools functional.

**Addresses from FEATURES.md:** All 7 table stakes features (semantic search, CRUD, list, configurable embedding entry point, stdio transport, Qdrant config, YAML preservation)

**Avoids:** Pitfall #1 (stdout), Pitfall #3 (lossy YAML), Pitfall #6 (collection initialization), Pitfall #7 (Aspire test infrastructure — get this working now before features pile up)

**Research flag:** Standard patterns — well-documented MCP C# SDK and Qdrant Aspire integration. Skip phase research.

### Phase 2: Embedding Providers and Search Quality

**Rationale:** The embedding abstraction must be designed as an interface before any provider is implemented (Pitfall #4). The dimension mismatch problem (Pitfall #2) must be solved here. Session tracking depends on search being stable. Search calibration (Pitfall #9) requires real embedding output to tune.

**Delivers:** Full IEmbeddingProvider abstraction with at least two concrete providers (OpenAI + one local option: ONNX or Ollama). Startup dimension validation. Session tracking with TTL and "already loaded" awareness in search results. Tuned similarity thresholds per provider. Archive (soft-delete) operation.

**Addresses from FEATURES.md:** Session tracking, archive, additional local embedding providers, configurable thresholds

**Avoids:** Pitfall #2 (dimension mismatch), Pitfall #4 (leaky abstraction), Pitfall #5 (session lifecycle), Pitfall #9 (score miscalibration)

**Research flag:** Embedding provider batching APIs may need per-provider research, particularly for ONNX runtime integration. Flag for phase research if ONNX is included.

### Phase 3: CLI, Developer Experience, and Distribution

**Rationale:** `--console` mode and `--setup` wizard share the same underlying services as MCP tools and only make sense once those services are stable. NuGet packaging is the final distribution step. These are the ecosystem differentiators that depend on a complete core.

**Delivers:** `--console` mode (single-shot JSON output + REPL), `--setup` wizard for 7+ agents with backup/dry-run safeguards, `--names` and `--summaries` output modes, bundled SKILL.md, `--reindex` migration command, NuGet tool package deployable via `dnx QdrantSkillsMCP`.

**Addresses from FEATURES.md:** `--setup` multi-agent configuration, `--names`/`--summaries` modes, bundled SKILL.md, console mode, NuGet distribution, skills-guru integration (if in scope)

**Avoids:** Pitfall #8 (dnx packaging misconfigurations — single framework target, full lifecycle test), Pitfall #12 (config file corruption — backup, dry-run, fallback to manual snippet)

**Research flag:** Agent config file formats (Claude, Copilot, Codex, OpenCode) change between versions. Needs targeted research during phase planning to verify current config schemas for each agent.

### Phase 4: Authentication and Enterprise Readiness

**Rationale:** Auth is explicitly deferred by FEATURES.md and PITFALLS.md recommends API key first with OAuth deferred. Implementing auth before the core is stable adds complexity that delays value delivery. API key auth is a small, well-understood addition; OAuth/OIDC is only needed for enterprise shared deployments.

**Delivers:** API key authentication (bearer token middleware). OAuth/OIDC (optional, if enterprise demand exists). Security review of the full surface area.

**Addresses from FEATURES.md:** Dual auth feature (API key + OAuth/OIDC)

**Avoids:** Pitfall #11 (OAuth over-engineering early)

**Research flag:** OAuth/OIDC integration with .NET middleware and MCP's transport model is non-trivial. Flag for phase research before attempting implementation.

### Phase Ordering Rationale

- The dependency graph from FEATURES.md is unambiguous: Qdrant connection and embedding provider are prerequisites for every feature. Core interfaces come before implementations.
- The architecture build order (Core → Infrastructure → Executable → Tests) directly maps to Phase 1 establishing Core, Phase 2 completing Infrastructure, Phase 3 completing the executable surface, Phase 4 adding security.
- Session tracking logically follows search stability (Phase 2) rather than being built speculatively in Phase 1.
- The three critical pitfalls (stdout, dimension mismatch, lossy YAML) are all Phase 1 concerns — getting them right at the start prevents cascading failures.
- Distribution (NuGet/dnx) and developer convenience (--setup, --console) are correctly last among core features — they wrap stable services.

### Research Flags

Phases needing deeper research during planning:
- **Phase 2 (ONNX embedding provider):** ONNX runtime integration in .NET 10 with bundled model files has gotchas around model packaging in NuGet tools. Research specific ONNX model file bundling approach if local-first embedding is a priority.
- **Phase 3 (agent config schemas):** Claude, Copilot, Codex, OpenCode, Docker Agent, Kilocode, and Factory Droid config formats and file locations should be verified against their current documentation before implementation. These change frequently.
- **Phase 4 (OAuth/OIDC with MCP stdio):** The interaction between OAuth token validation and the stdio transport model is not well-documented. Research required before Phase 4 planning.

Phases with standard patterns (skip research-phase):
- **Phase 1:** All patterns are well-documented in official Microsoft and Qdrant docs. MCP C# SDK, Aspire Qdrant integration, and YamlDotNet usage are straightforward.
- **Phase 2 (OpenAI + Ollama embedding providers):** Standard API patterns via Microsoft.Extensions.AI.OpenAI and OllamaSharp. No research needed.
- **Phase 3 (NuGet tool packaging):** Standard `PackAsTool` pattern, well-documented in STACK.md.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All package versions verified on NuGet. Official SDK docs confirm patterns. No experimental dependencies. One correction required: Aspire 9.2 → 13.x. |
| Features | HIGH | Five competitors analyzed. Table stakes derived from competitive landscape. Differentiators validated against gaps in existing tools. |
| Architecture | HIGH | Patterns sourced from official MCP C# SDK documentation and .NET Blog. Component boundaries match the SDK's own examples. Build order derived from actual project dependencies. |
| Pitfalls | HIGH | Pitfalls sourced from real GitHub issues (Aspire health check #5768, dnx #51831, #49815), real-world blog posts, and Qdrant documentation. stdout pollution is a confirmed, widely reported issue. |

**Overall confidence:** HIGH

### Gaps to Address

- **ONNX model bundling in NuGet tools:** STACK.md lists SmartComponents.LocalEmbeddings and Microsoft.ML.OnnxRuntime as options. The correct approach for bundling a model file inside a `PackAsTool` NuGet package for `dnx` consumption was not fully resolved. Address during Phase 2 planning if local-first embedding is prioritized.

- **skills-guru integration protocol:** The push/sync interface with skills-guru is mentioned as a Phase 3 feature but the skills-guru API/format was not researched. Needs discovery before Phase 3 planning if this integration is in scope.

- **MCP session ID availability:** The session tracking feature relies on a per-connection session identifier. The MCP C# SDK's mechanism for exposing the connection/session ID to tool methods was not fully confirmed in the research. Verify SDK capabilities before designing the SessionTracker interface.

- **Aspire 13.x Qdrant health check status:** GitHub issue #5768 documents missing health checks in Aspire's Qdrant integration. The resolution status of this issue as of Aspire 13.x was not confirmed. Verify during Phase 1 test infrastructure setup.

## Sources

### Primary (HIGH confidence)
- [NuGet: ModelContextProtocol 1.1.0](https://www.nuget.org/packages/ModelContextProtocol/) — version, download count, package identity
- [GitHub: modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — official SDK patterns, tool registration
- [Microsoft .NET Blog: Build MCP server in C#](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/) — WithTools/WithStdio pattern
- [NuGet: Qdrant.Client 1.17.0](https://www.nuget.org/packages/Qdrant.Client) — version, gRPC transport
- [Aspire Qdrant integration docs](https://learn.microsoft.com/en-us/dotnet/aspire/database/qdrant-component) — hosting + client setup
- [NuGet: Aspire.Hosting.Qdrant 13.1.0 / Aspire.Qdrant.Client 13.1.2 / Aspire.Hosting.Testing 13.2.0](https://www.nuget.org/packages/Aspire.Hosting.Qdrant) — version verification
- [Microsoft .NET Blog: AI and Vector Data Extensions GA](https://devblogs.microsoft.com/dotnet/ai-vector-data-dotnet-extensions-ga/) — M.E.AI GA announcement
- [NuGet: OllamaSharp 5.4.16](https://www.nuget.org/packages/OllamaSharp) — implements IEmbeddingGenerator, deprecates M.E.AI.Ollama
- [NuGet: xunit.v3 3.2.2](https://www.nuget.org/packages/xunit.v3) — MTP integration
- [Qdrant Official MCP Server](https://github.com/qdrant/mcp-server-qdrant) — competitive analysis
- [K-Dense Claude Skills MCP](https://github.com/K-Dense-AI/claude-skills-mcp) — competitive analysis
- [SkillSync MCP](https://github.com/adianmasood/skillsync-mcp) — competitive analysis
- [MCP C# SDK Official Documentation](https://csharp.sdk.modelcontextprotocol.io/) — architecture patterns
- [Qdrant health check issue in Aspire](https://github.com/dotnet/aspire/issues/5768) — known gap
- [Andrew Lock: dnx tool runner in .NET 10](https://andrewlock.net/exploring-dotnet-10-preview-features-5-running-one-off-dotnet-tools-with-dnx/) — packaging details

### Secondary (MEDIUM confidence)
- [skill-mcp Rust crate](https://lib.rs/crates/skill-mcp) — competitive analysis (Rust ecosystem)
- [MCP stdio transport explained](https://medium.com/@laurentkubaski/understanding-mcp-stdio-transport-protocol-ae3d5daf64db) — stdout/stderr conflict
- [Dealing with Vector Dimension Mismatch in Qdrant](https://medium.com/@epappas/dealing-with-vector-dimension-mismatch-my-experience-with-openai-embeddings-and-qdrant-vector-20a6e13b6d9f) — real-world dimension mismatch experience
- [mjm.local.docs - .NET MCP with Pluggable Embeddings](https://medium.com/@markjackmilian/net-open-source-local-knowledge-base-with-mcp-semantic-search-and-pluggable-embeddings-981c135ee3e7) — reference architecture
- [Server Tools - MCP C# SDK DeepWiki](https://deepwiki.com/modelcontextprotocol/csharp-sdk/2.1-server-tools) — tool registration patterns

### Tertiary (LOW confidence)
- [Concurrent dnx installation issue](https://github.com/dotnet/sdk/issues/51831) — known bug, may be resolved by .NET 10 GA
- [dnx preview version issue](https://github.com/dotnet/sdk/issues/49815) — may be resolved; verify during Phase 3

---
*Research completed: 2026-03-25*
*Ready for roadmap: yes*
