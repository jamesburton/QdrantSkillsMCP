# Phase 2: Search Intelligence and Embedding Providers - Context

**Gathered:** 2026-03-25
**Status:** Ready for planning

<domain>
## Phase Boundary

Session-aware search with progressive output modes (full, names-only, summaries-only) and pluggable embedding providers (ONNX local, Ollama, Azure OpenAI) with dimension safety validation. OpenAI provider already exists from Phase 1. Auth, CLI, distribution, and bundled skill are out of scope.

</domain>

<decisions>
## Implementation Decisions

### Embedding provider selection
- Single `EmbeddingProvider` config key in `QdrantSkillsOptions`: `"LocalONNX"`, `"OpenAI"`, `"Ollama"`, `"AzureOpenAI"`
- **Default behavior (no provider set):** Default to LocalONNX with a warning recommending the user explicitly set `"EmbeddingProvider": "LocalONNX"` (or `QDRANT_SKILLS__EmbeddingProvider=LocalONNX`) to silence the warning
- Provider wiring happens in `ServiceRegistration` based on the config key value

### ONNX local provider
- Use Microsoft.Extensions.AI ONNX support for loading the model and providing the standard `IEmbeddingGenerator` interface
- **Model sourcing (three-tier):**
  1. Config path to custom `.onnx` file (explicit override)
  2. Companion NuGet package with the recommended default model (e.g., `QdrantSkillsMCP.Models.DefaultEmbedding`) for pre-installation
  3. Auto-download from HuggingFace on first use if model file is missing (with opt-out toggle `DisableAutoDownload`)
- Default model: all-MiniLM-L6-v2 (or equivalent small, high-quality model — Claude's discretion on exact model)

### Ollama provider
- **Inside Aspire (AppHost):** Always spin up Ollama container via Aspire integration with routing. No port probing needed.
- **Outside Aspire (standalone):** Default to `http://localhost:11434`. If `EmbeddingProvider=Ollama` is set without `EmbeddingUrl`, assume user manages Ollama themselves — use default URL with a warning, no reachability checks.
- Config keys: `EmbeddingUrl` (or `QDRANT_SKILLS__EmbeddingUrl`), `EmbeddingModel` (model name for Ollama)

### Azure OpenAI provider
- Standard Azure OpenAI config: endpoint URL + API key + deployment name
- Uses `Microsoft.Extensions.AI` Azure OpenAI integration (same `IEmbeddingGenerator` interface)
- Claude's discretion on exact config key names (follow Azure SDK conventions)

### Output modes (names/summaries)
- Replace `includeContent` boolean with `outputMode` enum parameter on both `search-skills` and `list-skills`
- Values: `"full"` (default), `"names"`, `"summaries"`
  - `"full"`: returns full skill content (name, description, tags, score, raw_content) — current behavior
  - `"names"`: returns skill names only (string array)
  - `"summaries"`: returns name + description per skill (metadata without raw_content)
- Breaking change from Phase 1's `includeContent` — acceptable, pre-release
- Session tracking: only `"full"` mode marks skills as loaded. `"names"` and `"summaries"` do NOT mark as loaded (consistent with Phase 1's `includeContent: false` behavior)

### Dimension mismatch handling
- **Startup check (IHostedService):** Validate embedding dimensions on application startup, before MCP tools become available
- **Default behavior:** Hard fail with clear guidance: "Collection 'skills' has 1536-dim vectors but provider 'onnx' produces 384-dim."
- **Resolution flags:**
  - `--rename-mismatched`: rename old collection (e.g., `skills` -> `skills-old-1536`) and create new one
  - `--model-suffix`: use dimension-suffixed collection name (e.g., `skills-384`)
  - `--replace-mismatched`: delete old collection and create fresh (data loss, explicit opt-in)
- **Embedding output validation:** On first run, generate a test embedding from configurable input to verify dimensions
  - Default test key: `"test"`, default input: `"This is a test embedding input string."`
  - Stored in Qdrant but excluded from searches (e.g., `_test: true` payload filter)
  - `--skip-embedding-output-validation` to bypass the check
  - `--test-embedding-key` and `--test-embedding-input` to override defaults

### Session ID override
- Add optional `sessionId` parameter to `search-skills` and `load-skill`
- If omitted: uses default process-scoped session (Phase 1 behavior)
- If provided: tracks loaded skills per that session ID
- `InMemorySessionTracker` evolves to support keyed sessions (ConcurrentDictionary<string, HashSet<string>>)
- In-memory only — sessions clear on process restart
- New `reset-session` MCP tool: clears the loaded-skills list for a given session (or default session if no ID)

### Claude's Discretion
- Exact ONNX default model selection (all-MiniLM-L6-v2 or similar)
- Azure OpenAI config key naming (follow Azure SDK conventions)
- Internal structure of the provider factory/selector pattern
- OllamaSharp vs Microsoft.Extensions.AI.Ollama package choice
- Exact test embedding exclusion filter implementation
- Aspire Ollama integration details (container image, port mapping)

</decisions>

<specifics>
## Specific Ideas

- **Zero-config experience is the north star**: a user who installs the NuGet tool and runs it should get working semantic search without setting any API keys. LocalONNX with auto-download enables this.
- **Warnings, not errors for defaults**: when auto-detecting provider, always warn with the explicit config to set. Silent defaults are confusing; loud errors prevent getting started.
- **Companion model package**: `QdrantSkillsMCP.Models.DefaultEmbedding` NuGet package ships the default ONNX model. Users who want zero-download install both packages.
- **Test embedding as canary**: the stored test embedding serves as a dimension canary — on every startup, verify it still matches the configured provider's output dimensions.

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IEmbeddingService` interface (`Core/Interfaces/IEmbeddingService.cs`): `GenerateEmbeddingAsync` + `Dimensions` property — all new providers implement this
- `OpenAiEmbeddingService` (`Infrastructure/Embedding/OpenAiEmbeddingService.cs`): reference implementation for new providers
- `ServiceRegistration.AddQdrantSkillsInfrastructure()`: currently hardcodes OpenAI — needs provider selection logic
- `QdrantSkillsOptions`: needs `EmbeddingProvider`, `EmbeddingUrl`, `DisableAutoDownload`, and Azure-specific fields
- `CollectionInitializer`: currently does lazy init — dimension check should run before this on startup
- `InMemorySessionTracker`: singleton with `ConcurrentDictionary<string, byte>` — needs to become keyed by session ID

### Established Patterns
- `[McpServerToolType]` classes with constructor DI (SkillCrudTools, SkillSearchTools)
- All logging to stderr via `LogToStandardErrorThreshold = LogLevel.Trace`
- JSON serialization with `System.Text.Json` (camelCase, ignore nulls)
- Config binding: `IOptions<QdrantSkillsOptions>` injected via DI

### Integration Points
- `ServiceRegistration.cs`: provider selection switch statement based on `QdrantSkillsOptions.EmbeddingProvider`
- `SkillSearchTools.SearchSkills()`: replace `includeContent` parameter with `outputMode`
- `SkillSearchTools.ListSkills()`: add `outputMode` parameter
- `InMemorySessionTracker`: add session ID key support
- `Program.cs`: no changes needed (tools auto-discovered, services registered via DI)
- `AppHost/Program.cs`: add conditional Ollama container when provider is Ollama

</code_context>

<deferred>
## Deferred Ideas

- None — discussion stayed within phase scope.

</deferred>

---

*Phase: 02-search-intelligence-and-embedding-providers*
*Context gathered: 2026-03-25*
