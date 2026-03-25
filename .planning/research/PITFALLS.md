# Domain Pitfalls

**Domain:** .NET MCP server with Qdrant vector database for skill management
**Researched:** 2026-03-25

## Critical Pitfalls

Mistakes that cause rewrites, data loss, or fundamental architecture failure.

### Pitfall 1: stdout Pollution Breaks MCP stdio Transport

**What goes wrong:** Any `Console.WriteLine`, debug output, or logging framework writing to stdout corrupts the MCP JSON-RPC message stream. The MCP client receives malformed JSON, throws parse errors like "Unexpected token," and the connection dies silently or crashes.

**Why it happens:** .NET defaults to stdout for console output. Developers add `Console.WriteLine` for debugging, or a logging framework (Serilog, NLog) defaults to console sink. Even a library dependency printing a warning to stdout will break the protocol.

**Consequences:** MCP server appears to start but immediately fails. Error messages are cryptic because the transport itself is broken. Users blame the MCP client, not the server.

**Prevention:**
- Redirect ALL logging to stderr (`Console.Error.WriteLine`) or file-based sinks from day one
- Configure the DI logging pipeline to use stderr exclusively before any other setup
- Never use `Console.WriteLine` anywhere in the codebase -- enforce via analyzer or code review
- Add an integration test that captures stdout and asserts only valid JSON-RPC messages appear

**Detection:** MCP client reports "parse error" or "unexpected token." Server works via `--console` mode but fails via `--stdio`.

**Phase:** Must be addressed in Phase 1 (MCP server foundation). Non-negotiable from first line of code.

---

### Pitfall 2: Embedding Dimension Mismatch Corrupts the Collection

**What goes wrong:** User switches embedding provider (e.g., from a local ONNX model producing 384-dim vectors to OpenAI text-embedding-3-small producing 1536-dim vectors) and the existing Qdrant collection has vectors of the wrong dimension. Qdrant rejects upserts with a dimension mismatch error, or worse -- if the collection was auto-created with the first provider's dimensions, all existing data becomes unsearchable.

**Why it happens:** Qdrant collections are created with a fixed vector dimension. The "pluggable embedding provider" requirement means users WILL change providers. The collection dimension is set at creation time and cannot be changed without recreating the collection.

**Consequences:** All previously embedded skills become invalid. Users must re-embed their entire skill library. If there is no migration path, they lose data or must manually export/reimport.

**Prevention:**
- Store the embedding model identifier and dimension in collection metadata (Qdrant payload or a separate config point)
- On startup, validate that the configured embedding provider's dimension matches the collection's dimension
- If mismatch detected, refuse to start with a clear error message explaining the situation and offering migration options
- Provide a `--reindex` command that re-embeds all skills with the current provider
- Store the raw skill content (markdown + frontmatter) in the Qdrant payload so re-embedding never loses data

**Detection:** Qdrant client throws dimension mismatch on upsert. Search returns zero results despite skills existing.

**Phase:** Must be designed in Phase 1 (data model), implemented in Phase 2 (embedding provider layer). The `--reindex` command can come in Phase 3.

---

### Pitfall 3: Lossy Skill Format Transformation

**What goes wrong:** When storing skills in Qdrant, the YAML frontmatter and markdown body get separated, normalized, or partially stored. On retrieval, the reconstructed skill differs from the original -- missing frontmatter fields, altered whitespace, reordered YAML keys, or lost markdown formatting.

**Why it happens:** Developers parse YAML frontmatter into structured fields and store them as Qdrant payload properties, then reconstruct the original format on read. YAML parsing is lossy (comments stripped, key order lost, multiline strings normalized). The project constraint explicitly states "no lossy transformations."

**Consequences:** Skills returned to agents differ from what was stored. Agents that depend on exact formatting break. Round-trip fidelity is lost, violating the core constraint.

**Prevention:**
- Store the COMPLETE original skill text as a single string field in the Qdrant payload (`raw_content`)
- Parse frontmatter INTO structured payload fields for filtering/search, but always return the raw original
- Write a round-trip test: store a skill, retrieve it, assert byte-for-byte equality of the content
- Include edge cases: skills with YAML comments, multiline descriptions, special characters, empty frontmatter

**Detection:** Diff original skill against retrieved skill. Any difference is a bug.

**Phase:** Phase 1 (data model design). The storage schema must be right from the start.

---

### Pitfall 4: Embedding Provider Abstraction Leaks

**What goes wrong:** The "pluggable embedding provider" abstraction becomes a lowest-common-denominator interface that either (a) doesn't handle provider-specific quirks (rate limits, token limits, batching), or (b) leaks provider internals into the core logic, making it impossible to swap providers cleanly.

**Why it happens:** Embedding providers differ significantly: OpenAI has rate limits and requires API keys; local ONNX models need model file paths and have no rate limits but are CPU-bound; Ollama requires a running server. A naive `IEmbeddingProvider.EmbedAsync(string text)` interface ignores batching, chunking, token limits, and error handling that differ per provider.

**Consequences:** Provider swap requires touching core logic. One provider works, another silently produces bad embeddings (truncated text, wrong dimensions). Error handling is inconsistent.

**Prevention:**
- Design the interface around batched operations: `Task<float[][]> EmbedBatchAsync(string[] texts)`
- Each provider handles its own chunking, rate limiting, and retry logic internally
- Define a provider capabilities contract: `int MaxTokens`, `int Dimensions`, `bool SupportsBatching`
- Validate provider output dimensions against expected dimensions on first call
- Include a `TestProvider` for unit tests that returns deterministic fake embeddings

**Detection:** Adding a new provider requires changes outside the provider class. Tests pass with one provider but fail with another.

**Phase:** Phase 2 (embedding layer). Design the interface before implementing any provider.

## Moderate Pitfalls

### Pitfall 5: Session Tracking Tied to Wrong Lifecycle

**What goes wrong:** Session tracking (which skills have been returned) is tied to the MCP connection lifecycle, but MCP connections can be short-lived (per-request in some clients) or very long-lived (persistent connection in Claude Code). The "already loaded" feature either never triggers (new session each request) or accumulates stale state (hours-old session).

**Prevention:**
- Default to MCP connection lifecycle but support explicit session ID override (already in requirements)
- Implement session expiry with configurable TTL (e.g., 30 minutes of inactivity)
- Use in-memory `ConcurrentDictionary<string, SessionState>` -- no need for persistence
- Log session creation/expiry for debugging
- Clean up expired sessions via a background timer, not on every request

**Phase:** Phase 2 (session management). Design after MCP transport is stable.

---

### Pitfall 6: Qdrant Collection Not Created Before First Use

**What goes wrong:** Server starts, receives first `search-skills` call, and Qdrant throws "collection not found." Or the collection exists but with wrong configuration (no payload indexes, wrong distance metric). Developer adds auto-creation but doesn't set up payload indexes, leading to slow filtered searches.

**Prevention:**
- On startup, ensure collection exists with correct configuration (vector size from configured provider, cosine distance, payload indexes on frontmatter fields like `name`, `tags`, `archived`)
- Create payload indexes BEFORE data is inserted (Qdrant builds HNSW filter links during index creation)
- Use `CreateCollectionAsync` with `on_conflict: skip` or check existence first
- Log collection status on startup (created, verified, or error)

**Phase:** Phase 1 (Qdrant connection). Collection setup is part of server initialization.

---

### Pitfall 7: Aspire Test Infrastructure Flakiness

**What goes wrong:** Integration tests using Aspire's `DistributedApplicationTestingBuilder` with Qdrant containers are slow to start, flaky due to container readiness timing, and fail in CI due to Docker availability or port conflicts.

**Prevention:**
- Use `ResourceNotificationService.WaitForResourceAsync` to wait for Qdrant readiness -- do not use arbitrary `Task.Delay`
- Always call `DisposeAsync` on the `DistributedApplication` (use `IAsyncLifetime` in xUnit v3) to prevent port/container leaks
- Note: Aspire's Qdrant integration has historically lacked health checks (GitHub issue #5768) -- implement a custom health check that calls Qdrant's health endpoint
- For unit tests, mock the Qdrant client; use Aspire only for true integration tests
- In CI, ensure Docker is available and consider test parallelism limits to avoid resource exhaustion

**Phase:** Phase 1 (test infrastructure). Get Aspire + Qdrant tests working before building features on top.

---

### Pitfall 8: dnx Tool Packaging Misconfigurations

**What goes wrong:** The NuGet tool package doesn't work with `dnx` due to incorrect `PackAsTool` settings, missing `ToolCommandName`, or framework targeting issues. Users install the tool but get cryptic .NET runtime errors. Package size bloats if multiple target frameworks are packed.

**Prevention:**
- Set `<PackAsTool>true</PackAsTool>` and `<ToolCommandName>QdrantSkillsMCP</ToolCommandName>` in the csproj
- Target a single framework (`net10.0`) to minimize package size -- dnx requires the SDK anyway
- Test the full lifecycle: `dotnet pack`, `dnx QdrantSkillsMCP --help`, verify it runs
- Be aware of the known issue with concurrent `dnx` installations fighting over files (dotnet/sdk#51831)
- Avoid preview package version confusion -- `dnx` may not find non-preview versions correctly (dotnet/sdk#49815)

**Phase:** Phase 3 (packaging and distribution). Build features first, package last.

---

### Pitfall 9: Search Temperature / Similarity Threshold Miscalibration

**What goes wrong:** The `search-skills` tool returns too many irrelevant results (threshold too low) or misses relevant skills (threshold too high). Users don't understand what "temperature" means in this context. Default values work for one embedding model but not another.

**Prevention:**
- Use "score threshold" not "temperature" in the Qdrant query -- temperature is an LLM concept, not a vector search concept. Name the parameter `min_score` or `similarity_threshold` in the tool interface
- Default threshold varies by embedding model (cosine similarity ranges differ). Store a recommended threshold per provider
- Return similarity scores with results so users/agents can calibrate
- Start with a generous default (e.g., 0.3 for cosine similarity) and let users tune
- Cap `max_results` with a sensible default (e.g., 5) to prevent overwhelming the agent context window

**Phase:** Phase 2 (search implementation). Requires working embeddings to calibrate.

## Minor Pitfalls

### Pitfall 10: MCP Tool Schema / Description Quality

**What goes wrong:** MCP tools with poor descriptions or overly complex parameter schemas confuse AI agents. Agents call tools with wrong parameters, miss optional parameters, or don't understand when to use `search-skills` vs `load-skill`.

**Prevention:**
- Write tool descriptions from the agent's perspective: "Search for skills by describing what you need in natural language"
- Keep parameter schemas simple -- prefer flat string/number parameters over nested objects
- Include example values in parameter descriptions
- Test with multiple agents (Claude Code, Copilot, Codex) to verify tool discovery and correct usage

**Phase:** Phase 2 (tool implementation). Iterate descriptions based on agent testing.

---

### Pitfall 11: OAuth/OIDC Over-Engineering Early

**What goes wrong:** Implementing OAuth/OIDC authentication before the core features work adds massive complexity (token refresh, JWKS endpoints, claim mapping, multiple identity providers). It delays the MVP and introduces hard-to-debug auth failures.

**Prevention:**
- Phase 1: No auth (local development)
- Phase 2: API key (bearer token) only -- simple `Authorization: Bearer <key>` header check
- Phase 3+: OAuth/OIDC for enterprise -- only after core features are stable
- Design the auth middleware as pluggable from the start (interface), but don't implement OAuth until needed

**Phase:** Explicitly defer to Phase 3 or later. API key auth in Phase 2.

---

### Pitfall 12: `--setup` Command Breaks Agent Config Files

**What goes wrong:** The auto-configuration command that writes MCP server entries into agent config files (claude, copilot, codex, etc.) corrupts existing configurations. JSON/YAML parsing drops comments, reorders keys, or overwrites user customizations. Different agents have different config formats and locations that change between versions.

**Prevention:**
- Read the existing config, modify only the MCP server entry, write back preserving structure
- Use a JSON parser that preserves formatting/comments where possible
- Always create a backup of the original config file before modification
- Implement a `--dry-run` flag that shows what would be changed without writing
- Fall back to printing a snippet the user can paste manually if the config format is unrecognized
- Keep agent config paths in a configuration file so they can be updated without code changes

**Phase:** Phase 3 (setup command). Core functionality first, convenience tooling later.

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| MCP server bootstrap | stdout pollution (#1) | stderr-only logging from line one |
| Qdrant data model | Lossy transformations (#3) | Store raw content, parse separately for payload |
| Qdrant data model | Dimension mismatch (#2) | Store model metadata, validate on startup |
| Embedding providers | Leaky abstraction (#4) | Design interface with batching and capabilities |
| Session tracking | Wrong lifecycle (#5) | Configurable TTL + session ID override |
| Search implementation | Score miscalibration (#9) | Per-provider defaults, expose scores |
| Aspire testing | Container flakiness (#7) | Proper readiness checks, async disposal |
| Tool packaging | dnx issues (#8) | Single framework target, full lifecycle testing |
| Setup command | Config corruption (#12) | Backup, dry-run, fallback to snippets |
| Authentication | Over-engineering (#11) | API key first, OAuth deferred |

## Sources

- [MCP stdio debugging and logging](https://www.mcpevals.io/blog/debugging-mcp-servers-tips-and-best-practices) -- stdout/stderr conflict documentation
- [MCP stdio transport explained](https://medium.com/@laurentkubaski/understanding-mcp-stdio-transport-protocol-ae3d5daf64db) -- protocol message format
- [Dealing with Vector Dimension Mismatch in Qdrant](https://medium.com/@epappas/dealing-with-vector-dimension-mismatch-my-experience-with-openai-embeddings-and-qdrant-vector-20a6e13b6d9f) -- dimension mismatch real-world experience
- [Common Pitfalls with Vector Databases](https://dagshub.com/blog/common-pitfalls-to-avoid-when-using-vector-databases/) -- general vector DB pitfalls
- [Qdrant Payload Indexing](https://qdrant.tech/documentation/manage-data/payload/) -- payload index best practices
- [Qdrant Filtering Guide](https://qdrant.tech/articles/vector-search-filtering/) -- filter-aware HNSW indexing
- [Qdrant health check issue in Aspire](https://github.com/dotnet/aspire/issues/5768) -- missing health checks
- [Qdrant with Aspire timeout discussion](https://github.com/microsoft/kernel-memory/discussions/915) -- Aspire integration issues
- [dnx tool packaging pitfalls](https://andrewlock.net/exploring-dotnet-10-preview-features-5-running-one-off-dotnet-tools-with-dnx/) -- dnx authoring guidance
- [Concurrent dnx installation issue](https://github.com/dotnet/sdk/issues/51831) -- known bug
- [MCP C# SDK releases](https://github.com/modelcontextprotocol/csharp-sdk/releases) -- SDK version tracking
- [MCP lifecycle specification](https://modelcontextprotocol.io/specification/2025-03-26/basic/lifecycle) -- session lifecycle
- [Embedding migration strategies](https://medium.com/@adnanmasood/embeddings-in-practice-a-research-implementation-guide-9dbf20961590) -- re-embedding guidance
- [Semantic Kernel ONNX embeddings in .NET](https://elguerre.com/2025/05/25/implementing-embeddings-via-onnx-with-semantic-kernel-for-local-rag-solutions-in-net/) -- local embedding provider patterns
- [.NET MCP with pluggable embeddings](https://medium.com/@markjackmilian/net-open-source-local-knowledge-base-with-mcp-semantic-search-and-pluggable-embeddings-981c135ee3e7) -- pluggable embedding architecture reference
