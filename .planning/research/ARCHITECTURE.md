# Architecture Research

**Domain:** .NET MCP Server with Vector Storage (Qdrant-backed Skill Management)
**Researched:** 2026-03-25
**Confidence:** HIGH

## Standard Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                      Transport Layer                            │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────┐    │
│  │  stdio (MCP) │  │ Console CLI  │  │  --setup Wizard    │    │
│  └──────┬───────┘  └──────┬───────┘  └────────┬───────────┘    │
│         │                 │                    │                │
├─────────┴─────────────────┴────────────────────┴────────────────┤
│                        Tool Layer                               │
│  ┌────────────┐ ┌────────────┐ ┌─────────────┐ ┌────────────┐  │
│  │ search     │ │ load       │ │ add/update   │ │ archive/   │  │
│  │ -skills    │ │ -skill     │ │ -skill       │ │ delete     │  │
│  └─────┬──────┘ └─────┬──────┘ └──────┬───────┘ └─────┬──────┘  │
│        │              │               │               │         │
├────────┴──────────────┴───────────────┴───────────────┴─────────┤
│                      Service Layer                              │
│  ┌──────────────────┐  ┌──────────────┐  ┌──────────────────┐   │
│  │ SkillService     │  │ SessionTracker│  │ SkillParser      │   │
│  │ (orchestration)  │  │ (per-conn)   │  │ (YAML+MD)        │   │
│  └────────┬─────────┘  └──────────────┘  └──────────────────┘   │
│           │                                                     │
├───────────┴─────────────────────────────────────────────────────┤
│                    Abstraction Layer                             │
│  ┌──────────────────────────┐  ┌────────────────────────────┐   │
│  │ IEmbeddingProvider       │  │ ISkillRepository            │   │
│  │ (pluggable embeddings)   │  │ (Qdrant operations)         │   │
│  └────────┬─────────────────┘  └────────┬───────────────────┘   │
│           │                             │                       │
├───────────┴─────────────────────────────┴───────────────────────┤
│                    Infrastructure Layer                          │
│  ┌──────────────────────────┐  ┌────────────────────────────┐   │
│  │ Embedding Providers      │  │ QdrantClient               │   │
│  │ - ONNX (local)           │  │ (gRPC, port 6334)          │   │
│  │ - OpenAI API             │  │                            │   │
│  │ - Azure OpenAI           │  │                            │   │
│  │ - Ollama (local)         │  │                            │   │
│  └──────────────────────────┘  └────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| **MCP Server Host** | Lifecycle, DI container, transport selection | `Host.CreateApplicationBuilder` + `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()` |
| **MCP Tool Classes** | Expose operations to AI agents via MCP protocol | `[McpServerToolType]` classes with `[McpServerTool]` methods, constructor-injected dependencies |
| **Console CLI** | Single-shot commands and REPL for human use | `System.CommandLine` or manual arg parsing, shares services with MCP tools |
| **Setup Wizard** | Auto-configure agent MCP config files | Reads/writes JSON/YAML config for claude, copilot, codex, etc. |
| **SkillService** | Core business logic orchestrating search, CRUD | Coordinates between parser, embedding, repository, and session tracker |
| **SkillParser** | Parse/serialize skill format (YAML frontmatter + markdown) | YamlDotNet for frontmatter extraction, preserves raw markdown body |
| **SessionTracker** | Track which skills returned per MCP session | In-memory dictionary keyed by connection/session ID |
| **IEmbeddingProvider** | Abstract embedding generation | Interface with `Task<float[]> GenerateEmbeddingAsync(string text)` |
| **ISkillRepository** | Abstract vector store operations | Interface wrapping Qdrant upsert, search, get, delete |
| **QdrantSkillRepository** | Concrete Qdrant implementation | Uses `Qdrant.Client.QdrantClient` for gRPC operations on `skills` collection |
| **Embedding Providers** | Concrete embedding implementations | ONNX via `Microsoft.ML.OnnxRuntime`, OpenAI via HTTP, Ollama via HTTP |

## Recommended Project Structure

```
src/
├── QdrantSkillsMCP/                    # Main executable (NuGet tool)
│   ├── Program.cs                      # Host builder, DI wiring, transport selection
│   ├── Tools/                          # MCP tool definitions
│   │   ├── SearchSkillsTool.cs         # search-skills
│   │   ├── LoadSkillTool.cs            # load-skill
│   │   ├── AddSkillTool.cs             # add-skill
│   │   ├── UpdateSkillTool.cs          # update-skill
│   │   ├── ArchiveSkillTool.cs         # archive-skill
│   │   └── DeleteSkillTool.cs          # delete-skill
│   ├── Cli/                            # Console mode commands
│   │   ├── ConsoleRunner.cs            # --console entry point / REPL
│   │   └── SetupCommand.cs             # --setup wizard
│   └── appsettings.json                # Configuration (embedding provider, Qdrant endpoint)
│
├── QdrantSkillsMCP.Core/               # Domain models + interfaces (zero deps)
│   ├── Models/
│   │   ├── Skill.cs                    # Skill entity (name, description, tags, body, vector)
│   │   ├── SkillSearchResult.cs        # Search result with score
│   │   └── SkillSession.cs             # Session tracking model
│   ├── Interfaces/
│   │   ├── ISkillService.cs            # Core business operations
│   │   ├── ISkillRepository.cs         # Storage abstraction
│   │   ├── IEmbeddingProvider.cs       # Embedding generation abstraction
│   │   ├── ISkillParser.cs             # Skill format parsing
│   │   └── ISessionTracker.cs          # Session state
│   └── Options/
│       ├── QdrantOptions.cs            # Qdrant connection config
│       └── EmbeddingOptions.cs         # Embedding provider config
│
├── QdrantSkillsMCP.Infrastructure/     # Concrete implementations
│   ├── Qdrant/
│   │   └── QdrantSkillRepository.cs    # ISkillRepository via Qdrant.Client
│   ├── Embeddings/
│   │   ├── OnnxEmbeddingProvider.cs    # Local ONNX (e.g., all-MiniLM-L6-v2)
│   │   ├── OpenAiEmbeddingProvider.cs  # OpenAI text-embedding-3-small
│   │   ├── AzureOpenAiEmbeddingProvider.cs
│   │   └── OllamaEmbeddingProvider.cs  # Local Ollama
│   ├── Parsing/
│   │   └── YamlFrontmatterParser.cs    # YAML frontmatter + markdown body
│   ├── Session/
│   │   └── InMemorySessionTracker.cs   # Per-connection session tracking
│   └── ServiceCollectionExtensions.cs  # DI registration helpers
│
├── QdrantSkillsMCP.AppHost/            # Aspire AppHost (dev orchestration)
│   └── Program.cs                      # AddQdrant("qdrant") + AddProject
│
├── QdrantSkillsMCP.ServiceDefaults/    # Aspire service defaults
│   └── Extensions.cs                   # OpenTelemetry, health checks, etc.
│
tests/
├── QdrantSkillsMCP.Tests/              # XUnit v3 (MTP) integration tests
│   ├── IntegrationTests/
│   │   ├── SearchSkillsTests.cs        # End-to-end search via Aspire test harness
│   │   ├── SkillCrudTests.cs           # Add/update/archive/delete
│   │   └── SessionTrackingTests.cs     # Session behavior
│   └── UnitTests/
│       ├── SkillParserTests.cs         # YAML frontmatter parsing
│       ├── SkillServiceTests.cs        # Business logic
│       └── EmbeddingProviderTests.cs   # Provider contract tests
```

### Structure Rationale

- **QdrantSkillsMCP (executable):** The NuGet tool entry point. Contains only transport concerns (MCP tools, CLI) and host wiring. Tool classes use constructor injection to receive services -- the MCP C# SDK creates a new instance per invocation and resolves dependencies from the DI container.
- **QdrantSkillsMCP.Core:** Zero-dependency project defining domain models and interfaces. This ensures business contracts are portable and testable without infrastructure concerns.
- **QdrantSkillsMCP.Infrastructure:** All concrete implementations behind Core interfaces. Embedding providers, Qdrant repository, YAML parsing. This is the only project that references external NuGet packages like `Qdrant.Client`, `Microsoft.ML.OnnxRuntime`, `YamlDotNet`.
- **QdrantSkillsMCP.AppHost:** Aspire orchestrator for local development. Runs Qdrant as a container via `Aspire.Hosting.Qdrant`. Not shipped in the NuGet tool.
- **QdrantSkillsMCP.ServiceDefaults:** Standard Aspire service defaults (health checks, telemetry). Referenced by the main project for consistent observability.

## Architectural Patterns

### Pattern 1: Pluggable Embedding via Strategy Pattern

**What:** Define `IEmbeddingProvider` interface, implement per-provider, select via configuration at startup.
**When to use:** Always -- this is a core requirement. Users have different constraints (offline, cost, latency).
**Trade-offs:** Slight startup complexity, but trivial with .NET DI. Massive flexibility gain.

**Example:**
```csharp
// Core interface
public interface IEmbeddingProvider
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
    int Dimensions { get; }
}

// Registration based on config
public static IServiceCollection AddEmbeddingProvider(
    this IServiceCollection services, IConfiguration config)
{
    var provider = config.GetValue<string>("Embedding:Provider");
    return provider switch
    {
        "OpenAI" => services.AddSingleton<IEmbeddingProvider, OpenAiEmbeddingProvider>(),
        "AzureOpenAI" => services.AddSingleton<IEmbeddingProvider, AzureOpenAiEmbeddingProvider>(),
        "Ollama" => services.AddSingleton<IEmbeddingProvider, OllamaEmbeddingProvider>(),
        "ONNX" or _ => services.AddSingleton<IEmbeddingProvider, OnnxEmbeddingProvider>(),
    };
}
```

**Note:** Consider also implementing `Microsoft.Extensions.AI.IEmbeddingGenerator<string, Embedding<float>>` for interop with the broader .NET AI ecosystem (Semantic Kernel, etc.).

### Pattern 2: MCP Tool Classes with Constructor Injection

**What:** Non-static tool classes decorated with `[McpServerToolType]`, receiving services via constructor injection. The MCP C# SDK v1.0+ creates a new instance per tool invocation and resolves constructor parameters from the DI container.
**When to use:** Always -- all tools need access to `ISkillService`, `ISessionTracker`, etc.
**Trade-offs:** Clean separation of concerns. Each tool class is small and focused.

**Example:**
```csharp
[McpServerToolType]
public class SearchSkillsTool
{
    private readonly ISkillService _skillService;
    private readonly ISessionTracker _sessionTracker;

    public SearchSkillsTool(ISkillService skillService, ISessionTracker sessionTracker)
    {
        _skillService = skillService;
        _sessionTracker = sessionTracker;
    }

    [McpServerTool, Description("Search skills by semantic similarity")]
    public async Task<string> SearchSkills(
        [Description("Natural language search query")] string query,
        [Description("Max results to return")] int maxResults = 5,
        [Description("Similarity threshold 0.0-1.0")] float temperature = 0.7f)
    {
        var results = await _skillService.SearchAsync(query, maxResults, temperature);
        var sessionId = _sessionTracker.CurrentSessionId;
        // Mark returned skills as loaded in session
        // Include "ALREADY LOADED SKILLS" header
        return JsonSerializer.Serialize(results);
    }
}
```

### Pattern 3: Dual-Mode Entry Point (stdio vs Console)

**What:** Single executable that switches behavior based on launch arguments: `--stdio` for MCP transport, `--console` for CLI mode, `--setup` for configuration wizard.
**When to use:** When the tool needs both agent-facing (MCP) and human-facing (CLI) interfaces.
**Trade-offs:** Slightly more complex `Program.cs`, but avoids shipping two separate executables.

**Example:**
```csharp
var builder = Host.CreateApplicationBuilder(args);

// Register shared services regardless of mode
builder.Services.AddSkillServices(builder.Configuration);

if (args.Contains("--setup"))
{
    builder.Services.AddHostedService<SetupWizardService>();
}
else if (args.Contains("--console"))
{
    builder.Services.AddHostedService<ConsoleRunnerService>();
}
else // Default: stdio MCP server
{
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();
}

await builder.Build().RunAsync();
```

### Pattern 4: Qdrant Payload-Based Storage

**What:** Store skill metadata (name, description, tags, archived flag) as Qdrant point payloads alongside the embedding vector. The full markdown body goes in payload too.
**When to use:** When skill data fits comfortably in Qdrant payloads (skills are typically <100KB).
**Trade-offs:** No secondary database needed. Qdrant is both the vector index and the document store. Simplifies architecture significantly. Trade-off: no relational queries, but skill data doesn't need them.

## Data Flow

### Search Flow (Primary)

```
Agent sends "search-skills" via MCP
    |
    v
SearchSkillsTool.SearchSkills(query, maxResults, temperature)
    |
    v
SkillService.SearchAsync(query, maxResults, temperature)
    |
    +--> IEmbeddingProvider.GenerateEmbeddingAsync(query)
    |        |
    |        v
    |    [Embedding vector: float[384] or float[1536]]
    |
    +--> ISkillRepository.SearchAsync(vector, maxResults, threshold)
    |        |
    |        v
    |    QdrantClient.SearchAsync("skills", vector, limit, scoreThreshold)
    |        |
    |        v
    |    [ScoredPoint[] with payloads]
    |
    +--> SessionTracker.MarkReturned(sessionId, skillNames)
    |
    v
Formatted results with "ALREADY LOADED SKILLS" header
    |
    v
JSON response back to agent via MCP
```

### Add/Update Flow

```
Agent sends "add-skill" with markdown content
    |
    v
AddSkillTool.AddSkill(name, content)
    |
    v
SkillService.AddAsync(name, content)
    |
    +--> ISkillParser.Parse(content)
    |        |
    |        v
    |    Skill { Name, Description, Tags, Body } from YAML frontmatter
    |
    +--> IEmbeddingProvider.GenerateEmbeddingAsync(embeddableText)
    |        |
    |        v
    |    [Embedding vector from description + body summary]
    |
    +--> ISkillRepository.UpsertAsync(skill, vector)
    |        |
    |        v
    |    QdrantClient.UpsertAsync("skills", [PointStruct])
    |
    v
Success confirmation back to agent
```

### Session Tracking Flow

```
MCP connection established
    |
    v
SessionTracker creates new session (connection ID or explicit override)
    |
    v
On each search-skills / load-skill response:
    SessionTracker.MarkReturned(sessionId, skillNames[])
    |
    v
On subsequent searches:
    Results include "ALREADY LOADED SKILLS: skill-a, skill-b"
    Already-loaded skills excluded or deprioritized
    |
    v
MCP connection closed → session discarded
```

### Key Data Flows

1. **Skill ingestion:** Raw markdown --> YAML parse --> extract embeddable text --> generate vector --> upsert to Qdrant with payload
2. **Semantic search:** Natural language query --> generate query vector --> Qdrant vector search --> filter/rank results --> session-aware response
3. **Skill retrieval:** Name lookup --> Qdrant scroll/filter by name payload --> return full markdown content
4. **Session awareness:** Per-connection tracking of returned skills --> reduces redundant skill loading

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| 1-100 skills | Single Qdrant instance, brute-force search is fine, ONNX local embeddings work great |
| 100-10K skills | HNSW index (Qdrant default), consider OpenAI embeddings for quality, add payload indexing on `name` and `tags` |
| 10K+ skills | Qdrant sharding/replicas, batch embedding pipeline, consider caching frequent queries |

### Scaling Priorities

1. **First bottleneck: Embedding latency.** Generating embeddings on every search query adds 50-500ms depending on provider. For local ONNX this is fast (~20ms). For API providers, network latency dominates. Mitigation: query embeddings are small and fast; skill embeddings are computed once at ingest time.
2. **Second bottleneck: Qdrant cold start.** Running Qdrant in a container adds startup time. For development, Aspire handles this. For production, use a persistent Qdrant instance with `WithLifetime(ContainerLifetime.Persistent)`.

## Anti-Patterns

### Anti-Pattern 1: Embedding Provider Hardcoding

**What people do:** Directly call OpenAI API from tool classes without abstraction.
**Why it's wrong:** Locks users into one provider. Some users need offline/local operation. Different environments have different constraints.
**Do this instead:** Define `IEmbeddingProvider` interface in Core. Implement per-provider in Infrastructure. Select via configuration.

### Anti-Pattern 2: Storing Skills Outside Qdrant

**What people do:** Use Qdrant only for vectors and store skill content in a separate SQLite/file database.
**Why it's wrong:** Adds a second data store to synchronize, increases operational complexity, and Qdrant payloads handle document storage perfectly well for this use case.
**Do this instead:** Store everything (metadata + full content) in Qdrant point payloads. Skills are small (<100KB). Qdrant handles this natively.

### Anti-Pattern 3: Static Tool Classes with Service Locator

**What people do:** Use static tool methods and resolve services via `IServiceProvider.GetService<T>()` inside the method body.
**Why it's wrong:** Hides dependencies, makes testing difficult, violates DI best practices.
**Do this instead:** Use non-static tool classes with constructor injection. The MCP C# SDK v1.0 fully supports this -- it creates instances per invocation and resolves constructor parameters from DI.

### Anti-Pattern 4: Monolithic Tool Class

**What people do:** Put all MCP tools in a single class with 6+ methods.
**Why it's wrong:** Violates SRP, makes testing harder, tool discovery documentation becomes cluttered.
**Do this instead:** One tool class per logical operation (or closely related pair). Each file is small, focused, and independently testable.

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| Qdrant | `Qdrant.Client` NuGet, gRPC on port 6334 | Aspire provides container hosting for dev; production uses standalone instance |
| OpenAI API | HTTP client, `text-embedding-3-small` model | API key via config or env var `OPENAI_API_KEY` |
| Azure OpenAI | HTTP client with Azure endpoint + key | Endpoint + key via config or env vars |
| Ollama | HTTP client, local endpoint (default 11434) | Models like `nomic-embed-text` for embeddings |
| ONNX Runtime | `Microsoft.ML.OnnxRuntime` in-process | Bundled model (e.g., `all-MiniLM-L6-v2`) for zero-dependency local operation |
| Agent Config Files | File system read/write | JSON config files for Claude (`~/.claude.json`), Copilot, Codex, etc. |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| Tool Layer <-> Service Layer | Direct DI injection | Tools receive `ISkillService` via constructor |
| Service Layer <-> Repository | `ISkillRepository` interface | Decouples business logic from Qdrant specifics |
| Service Layer <-> Embedding | `IEmbeddingProvider` interface | Hot-swappable at configuration time |
| AppHost <-> Main Project | Aspire resource references | Dev-time only; production runs standalone |
| MCP Transport <-> Tool Discovery | `WithToolsFromAssembly()` reflection | SDK handles serialization, schema generation |

## Build Order (Dependency Chain)

The following build order respects project dependencies and enables incremental development:

```
Phase 1: QdrantSkillsMCP.Core
    (models, interfaces -- no external deps, enables parallel work)
         |
         v
Phase 2: QdrantSkillsMCP.Infrastructure  +  QdrantSkillsMCP.AppHost
    (implementations depend on Core)     (Aspire setup, depends on nothing yet)
         |
         v
Phase 3: QdrantSkillsMCP (executable)
    (wires DI, defines tools, depends on Core + Infrastructure)
         |
         v
Phase 4: QdrantSkillsMCP.Tests
    (integration tests via Aspire testing, depends on all projects)
```

**Key dependency insight:** Core has zero external NuGet dependencies. Infrastructure depends on Core + external packages. The executable depends on Core + Infrastructure + MCP SDK. This clean layering means Core can be built and tested first, enabling parallel development of Infrastructure components.

## Sources

- [MCP C# SDK Official Documentation](https://csharp.sdk.modelcontextprotocol.io/) - HIGH confidence
- [Build a Model Context Protocol (MCP) server in C# - .NET Blog](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/) - HIGH confidence
- [MCP C# SDK v1.0 Release - InfoQ](https://www.infoq.com/news/2026/03/mcp-csharp-v1/) - HIGH confidence
- [MCP C# SDK GitHub](https://github.com/modelcontextprotocol/csharp-sdk) - HIGH confidence
- [Qdrant .NET SDK](https://github.com/qdrant/qdrant-dotnet) - HIGH confidence
- [Aspire Qdrant Integration](https://aspire.dev/integrations/databases/qdrant/qdrant-get-started/) - HIGH confidence
- [mjm.local.docs - .NET MCP with Pluggable Embeddings](https://medium.com/@markjackmilian/net-open-source-local-knowledge-base-with-mcp-semantic-search-and-pluggable-embeddings-981c135ee3e7) - MEDIUM confidence (reference architecture, not official)
- [Semantic Kernel ONNX Embeddings](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/src/Connectors/Connectors.Onnx/BertOnnxTextEmbeddingGenerationService.cs) - HIGH confidence
- [Server Tools - MCP C# SDK DeepWiki](https://deepwiki.com/modelcontextprotocol/csharp-sdk/2.1-server-tools) - MEDIUM confidence

---
*Architecture research for: .NET MCP Server with Qdrant-backed Skill Management*
*Researched: 2026-03-25*
