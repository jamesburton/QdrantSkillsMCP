# Phase 1: Core MCP Server - Context

**Gathered:** 2026-03-25
**Status:** Ready for planning

<domain>
## Phase Boundary

Working MCP server that agents can connect to via stdio. Full skill CRUD (add, update, delete, archive) and semantic search using OpenAI embeddings, backed by Qdrant. Aspire v13.2 AppHost for local dev with Qdrant container. XUnit v3 (MTP) test infrastructure. Auth, additional embedding providers, CLI/console mode, and --setup command are out of scope for this phase.

</domain>

<decisions>
## Implementation Decisions

### Configuration model
- Three-source configuration: appsettings.json / user secrets + environment variables + CLI args
- Precedence order: CLI args > environment variables > config file (standard .NET pattern)
- Two config file names supported: `appsettings.json` (standard .NET convention, supports user secrets + appsettings.Development.json) AND `qdrant-skills.json` (portable, tool-specific alternative)
- Config scopes: project-local config overrides user-global defaults (`~/.qdrant-skills/` or similar)
- Environment variable prefix: `QDRANT_SKILLS__` (double-underscore for .NET section nesting, e.g. `QDRANT_SKILLS__Qdrant__Host=localhost`)

### Skill identity & conflicts
- Skill identity: `name` field from YAML frontmatter is the primary key
- Qdrant point ID: deterministic hash (SHA256) of the skill name → stable UUID
- `add-skill` default behavior: error on duplicate name (explicit, safe)
- `add-skill --overwrite` flag: allows upsert when explicitly requested
- `update-skill`: always upserts (no conflict — name must exist)
- Skill name validation: enforce Claude Code skill format (lowercase letters, numbers, hyphens only, max 64 chars) — reject invalid names at add time

### Search result format
- `search-skills` default: returns full skill content (name, description, full markdown body, score)
- `includeContent: false` parameter: returns metadata only (name, description, tags, score) — agent must call `load-skill` separately to get content. Skills returned with `includeContent: false` are NOT added to the session's already-loaded list (content was not fetched)
- Similarity score: included per-result (0-1 float)
- Result ordering: score descending, with recency tiebreaker (recently updated skills rank higher on score ties)
- Session tracking "ALREADY LOADED SKILLS": returned both as a structured `alreadyLoaded: [string[]]` field AND as a text prefix at the top of the response (`ALREADY LOADED SKILLS: skill-a, skill-b\n\n{results}`) — supports both prose-parsing and JSON-parsing agents

### Solution structure
- Projects:
  - `QdrantSkillsMCP.Core` — interfaces, models, zero external dependencies
  - `QdrantSkillsMCP.Infrastructure` — Qdrant client, embedding providers, YAML parsing, MCP tool implementations
  - `QdrantSkillsMCP.AppHost` — Aspire AppHost (Qdrant container, dev orchestration)
  - `QdrantSkillsMCP.UnitTests` — unit tests (no external service dependencies)
  - `QdrantSkillsMCP.IntegrationTests` — integration tests using Aspire testing framework + Qdrant container
- NuGet tool entry point: Claude's discretion — standard .NET tool packaging convention
- MCP tool classes: one `[McpServerToolType]` class per logical group (SkillCrudTools, SkillSearchTools, etc.) using DI-injected instances

### Claude's Discretion
- NuGet tool entry point project structure (follow established .NET convention)
- Exact YAML parsing library choice (YamlDotNet is the standard, verify compatibility)
- Logging implementation details (all to stderr — never stdout, which is reserved for MCP JSON-RPC)
- Qdrant payload schema — what fields to index beyond name and archived flag
- MCP session ID strategy — verify if MCP C# SDK exposes session ID; implement connection-scoped tracking if available

</decisions>

<specifics>
## Specific Ideas

- **Stdout pollution is the #1 risk**: all logging, diagnostics, and debug output MUST go to stderr. Console.Out → MCP JSON-RPC transport. Console.Error → developer logs. This is non-negotiable.
- **YAML round-trip fidelity**: store raw skill content alongside parsed metadata in Qdrant payload. Reconstruct the original markdown+frontmatter on retrieval — no lossy transformation.
- **Aspire Qdrant health check gap**: research flagged a known issue (GitHub #5768) where the Qdrant Aspire integration may lack proper health checks. Integration tests should include a custom readiness check/wait strategy.
- **`includeContent: false` and session tracking**: skills fetched metadata-only are not marked as loaded — the agent hasn't seen the content yet. Only full-content fetches (search with default, or load-skill) mark skills as loaded in the session.

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- None yet — greenfield project

### Established Patterns
- .NET 10 + Aspire 13.2: use `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()` pattern from ModelContextProtocol C# SDK v1.1.0
- Qdrant.Client v1.17.0: official .NET client, use `QdrantClient` with async operations
- Microsoft.Extensions.AI v10.4.x: `IEmbeddingGenerator<string, Embedding<float>>` as the embedding abstraction

### Integration Points
- MCP stdio transport: `Program.cs` sets up `IHostBuilder` → `AddMcpServer().WithStdioServerTransport()`
- Aspire AppHost: `AddQdrant("qdrant")` + `AddQdrantClient()` for dev container management
- DI wiring: `IQdrantSkillRepository`, `IEmbeddingProvider`, `ISessionTracker` → registered in DI, injected into tool classes

</code_context>

<deferred>
## Deferred Ideas

- None — discussion stayed within phase scope. Auth (API key + OAuth) is already tracked in v2 requirements.

</deferred>

---

*Phase: 01-core-mcp-server*
*Context gathered: 2026-03-25*
