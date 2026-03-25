# Phase 1: Core MCP Server - Research

**Researched:** 2026-03-25
**Domain:** .NET MCP Server with Qdrant vector storage, skill CRUD, semantic search
**Confidence:** HIGH

## Summary

Phase 1 builds a greenfield .NET 10 MCP server that connects via stdio transport, persists skills (markdown with YAML frontmatter) in Qdrant with vector embeddings, and exposes full CRUD plus semantic search as MCP tools. The technology stack is well-defined: ModelContextProtocol C# SDK v1.1.0 for the MCP server, Qdrant.Client v1.17.0 for vector storage, Microsoft.Extensions.AI for the embedding abstraction (with OpenAI as the first provider), YamlDotNet for frontmatter parsing, and Aspire v13.x for local dev orchestration. XUnit v3 with MTP provides the test infrastructure.

The architecture follows standard .NET patterns: Host builder with DI, `[McpServerToolType]` classes with constructor-injected services, and Aspire AppHost for container management. The MCP C# SDK supports both static and instance tool methods with full DI injection. Qdrant supports both `ulong` and `Guid` point IDs natively, which aligns with the SHA256-to-UUID identity strategy from CONTEXT.md.

**Primary recommendation:** Build the solution with five projects (Core, Infrastructure, AppHost, UnitTests, IntegrationTests) using the standard MCP SDK patterns. Start with collection auto-creation and the embedding pipeline, then layer CRUD tools on top, then search. All logging must go to stderr -- stdout is reserved for MCP JSON-RPC.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Three-source configuration: appsettings.json / user secrets + environment variables + CLI args
- Precedence order: CLI args > environment variables > config file (standard .NET pattern)
- Two config file names supported: `appsettings.json` and `qdrant-skills.json`
- Config scopes: project-local config overrides user-global defaults
- Environment variable prefix: `QDRANT_SKILLS__`
- Skill identity: `name` field from YAML frontmatter is the primary key
- Qdrant point ID: deterministic hash (SHA256) of the skill name -> stable UUID
- `add-skill` default behavior: error on duplicate name (explicit, safe)
- `add-skill --overwrite` flag: allows upsert when explicitly requested
- `update-skill`: always upserts (no conflict -- name must exist)
- Skill name validation: lowercase letters, numbers, hyphens only, max 64 chars
- `search-skills` default: returns full skill content (name, description, full markdown body, score)
- `includeContent: false` parameter: returns metadata only
- Similarity score: included per-result (0-1 float)
- Result ordering: score descending, with recency tiebreaker
- Session tracking "ALREADY LOADED SKILLS": returned both as structured field and text prefix
- Projects: Core, Infrastructure, AppHost, UnitTests, IntegrationTests
- MCP tool classes: one `[McpServerToolType]` class per logical group with DI-injected instances

### Claude's Discretion
- NuGet tool entry point project structure (follow established .NET convention)
- Exact YAML parsing library choice (YamlDotNet is the standard)
- Logging implementation details (all to stderr)
- Qdrant payload schema -- what fields to index beyond name and archived flag
- MCP session ID strategy -- verify if MCP C# SDK exposes session ID

### Deferred Ideas (OUT OF SCOPE)
- None -- discussion stayed within phase scope. Auth is tracked in v2 requirements.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| MCP-01 | Server runs via `--stdio` flag for standard MCP stdio transport | MCP C# SDK `WithStdioServerTransport()` pattern verified |
| MCP-02 | Server exposes all skill tools as MCP tool endpoints discoverable by agents | `WithToolsFromAssembly()` auto-discovers `[McpServerToolType]` classes |
| QDR-01 | Connects to configurable Qdrant instance (default: localhost:6334) | `QdrantClient("localhost")` connects to 6334; configurable via DI |
| QDR-02 | Uses configurable collection name (default: `skills`) | Collection name passed as string parameter to all Qdrant operations |
| QDR-03 | Supports Qdrant API key for authenticated instances | `QdrantChannel.ForAddress()` with `ApiKey` in `ClientConfiguration` |
| QDR-04 | Auto-creates collection with correct vector dimensions on first use | `CreateCollectionAsync()` with `VectorParams { Size, Distance }` |
| CRUD-01 | `add-skill` persists skill with vector embedding | `UpsertAsync()` with `PointStruct` containing vectors + payload |
| CRUD-02 | `update-skill` updates content and re-generates embedding | Same upsert pattern; deterministic point ID ensures overwrite |
| CRUD-03 | `delete-skill` permanently removes skill | `DeleteAsync()` by point ID |
| CRUD-04 | `archive-skill` soft-hides without deletion | Set `archived: true` in payload; filter in search queries |
| CRUD-05 | YAML frontmatter and markdown body preserved losslessly | Store raw content in payload; reconstruct on retrieval |
| SRCH-01 | `search-skills` performs semantic vector search | `SearchAsync()` with embedding vector, limit, and score threshold |
| SRCH-02 | Configurable `temperature` parameter for matching threshold | Map temperature to Qdrant `score_threshold` parameter |
| SRCH-03 | Configurable `max-results` parameter | Maps to `limit` in `SearchAsync()` |
| SRCH-04 | `load-skill` retrieves specific skill(s) by name | `RetrieveAsync()` by deterministic UUID from skill name |
| SRCH-05 | `load-skill` supports reloading updated skills | Always fetches current version from Qdrant (no caching) |
| SRCH-06 | `list-skills` returns inventory of all skills | `ScrollAsync()` with filter excluding archived, returning payload metadata |
| EMB-01 | Configurable embedding provider via `IEmbeddingGenerator` abstraction | `IEmbeddingGenerator<string, Embedding<float>>` from Microsoft.Extensions.AI |
| EMB-02 | OpenAI embedding provider (text-embedding-3-small/large) | `Microsoft.Extensions.AI.OpenAI` + `AsEmbeddingGenerator(modelId)` |
| DIST-02 | Aspire AppHost runs Qdrant via Aspire integration | `AddQdrant("qdrant").WithLifetime(ContainerLifetime.Persistent)` |
| DIST-03 | Full XUnit v3 (MTP) test coverage using Aspire testing framework | `DistributedApplicationTestingBuilder` + xunit.v3 packages |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ModelContextProtocol | 1.1.0 | MCP server SDK (stdio transport, tool discovery) | Official SDK co-maintained with Microsoft |
| Microsoft.Extensions.Hosting | (built-in .NET 10) | Host builder, DI, configuration, logging | Standard .NET hosting pattern |
| Qdrant.Client | 1.17.0 | Qdrant vector database client (gRPC) | Official .NET client from Qdrant |
| Microsoft.Extensions.AI.Abstractions | 10.4.1 | `IEmbeddingGenerator<string, Embedding<float>>` interface | Microsoft's standard AI abstraction layer |
| Microsoft.Extensions.AI.OpenAI | 10.4.1 | OpenAI implementation of IEmbeddingGenerator | First-party Microsoft integration |
| YamlDotNet | 16.3.0 | YAML frontmatter parsing and serialization | De facto .NET YAML library, AOT-compatible |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Aspire.Hosting.Qdrant | 13.1.0+ | Qdrant container in AppHost | AppHost project only |
| Aspire.Qdrant.Client | 13.1.2+ | DI registration for QdrantClient from Aspire | Infrastructure project for Aspire integration |
| Aspire.Hosting.Testing | 13.1.0+ | DistributedApplicationTestingBuilder | Integration test project only |
| xunit.v3 | 3.2.0+ | Test framework with MTP support | All test projects |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| YamlDotNet | SharpYaml | YamlDotNet has much larger community, AOT source generator, and is the standard |
| Qdrant.Client (gRPC) | Qdrant REST API via HttpClient | gRPC is the standard for .NET; higher performance, typed client |
| Microsoft.Extensions.AI | Direct OpenAI SDK | M.E.AI provides the abstraction needed for multi-provider support in Phase 2 |

**Installation (per project):**

Core project (zero external dependencies by design):
```bash
# No NuGet packages -- only interfaces and models
```

Infrastructure project:
```bash
dotnet add package ModelContextProtocol
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Qdrant.Client --version 1.17.0
dotnet add package Microsoft.Extensions.AI.OpenAI --version 10.4.1
dotnet add package YamlDotNet --version 16.3.0
dotnet add package Aspire.Qdrant.Client
```

AppHost project:
```bash
dotnet add package Aspire.Hosting.Qdrant
```

Integration test project:
```bash
dotnet add package Aspire.Hosting.Testing
dotnet add package xunit.v3
```

## Architecture Patterns

### Recommended Project Structure
```
QdrantSkillsMCP.sln
src/
  QdrantSkillsMCP.Core/           # Interfaces, models, zero dependencies
    Models/
      Skill.cs                    # Skill record (name, description, tags, content, etc.)
      SkillMetadata.cs            # Metadata-only projection for includeContent:false
      SearchResult.cs             # Skill + similarity score wrapper
    Interfaces/
      ISkillRepository.cs         # CRUD + search + list operations
      IEmbeddingService.cs        # Wraps IEmbeddingGenerator for domain use
      ISessionTracker.cs          # Tracks loaded skills per connection
    Validation/
      SkillNameValidator.cs       # Lowercase, hyphens, numbers, max 64 chars
  QdrantSkillsMCP.Infrastructure/ # Qdrant client, embedding, YAML, MCP tools
    Qdrant/
      QdrantSkillRepository.cs    # ISkillRepository implementation
      CollectionInitializer.cs    # Auto-create collection + payload indexes
    Embedding/
      OpenAiEmbeddingService.cs   # IEmbeddingService wrapping IEmbeddingGenerator
    Yaml/
      SkillParser.cs              # Parse frontmatter + body; reconstruct losslessly
    Session/
      InMemorySessionTracker.cs   # Per-connection loaded-skills tracking
    Tools/
      SkillCrudTools.cs           # [McpServerToolType] add, update, delete, archive
      SkillSearchTools.cs         # [McpServerToolType] search, load, list
    Configuration/
      QdrantSkillsOptions.cs      # Strongly-typed configuration POCO
    ServiceRegistration.cs        # DI extension methods
    Program.cs                    # Host builder entry point
  QdrantSkillsMCP.AppHost/        # Aspire orchestration
    Program.cs                    # AddQdrant + AddProject
    appsettings.json
tests/
  QdrantSkillsMCP.UnitTests/      # No external dependencies
    Yaml/
      SkillParserTests.cs
    Validation/
      SkillNameValidatorTests.cs
    Session/
      InMemorySessionTrackerTests.cs
  QdrantSkillsMCP.IntegrationTests/ # Aspire + Qdrant container
    SkillCrudIntegrationTests.cs
    SkillSearchIntegrationTests.cs
    CollectionInitializerTests.cs
```

### Pattern 1: MCP Tool Class with DI
**What:** Non-static `[McpServerToolType]` class with constructor-injected services
**When to use:** All MCP tool definitions
**Example:**
```csharp
// Source: MCP C# SDK docs + DeepWiki
[McpServerToolType]
public class SkillCrudTools
{
    private readonly ISkillRepository _repository;
    private readonly IEmbeddingService _embeddings;
    private readonly ISessionTracker _session;

    public SkillCrudTools(
        ISkillRepository repository,
        IEmbeddingService embeddings,
        ISessionTracker session)
    {
        _repository = repository;
        _embeddings = embeddings;
        _session = session;
    }

    [McpServerTool(Name = "add-skill", Destructive = true, ReadOnly = false),
     Description("Add a new skill to the Qdrant skills library.")]
    public async Task<string> AddSkill(
        string name,
        string content,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        // Validate name, parse YAML, generate embedding, upsert to Qdrant
    }
}
```

### Pattern 2: Deterministic Point ID from Skill Name
**What:** SHA256 hash of skill name converted to a stable UUID (Guid)
**When to use:** All Qdrant point operations
**Example:**
```csharp
// Source: CONTEXT.md decision + Qdrant docs (Guid IDs supported natively)
public static Guid GeneratePointId(string skillName)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(skillName));
    // Take first 16 bytes of SHA256 to form a Guid
    return new Guid(hash.AsSpan(0, 16));
}
```

### Pattern 3: YAML Frontmatter Parsing with Lossless Round-Trip
**What:** Parse frontmatter for metadata, store raw content for reconstruction
**When to use:** All skill add/update operations
**Example:**
```csharp
// Source: YamlDotNet docs + CONTEXT.md (lossless round-trip requirement)
public class SkillParser
{
    private static readonly string FrontmatterDelimiter = "---";

    public (SkillFrontmatter metadata, string rawContent) Parse(string skillContent)
    {
        // Split on "---" delimiters
        // Parse YAML block with YamlDotNet deserializer
        // Return parsed metadata AND original raw content
        // Store raw content in Qdrant payload for lossless retrieval
    }

    public string Reconstruct(SkillFrontmatter metadata, string markdownBody)
    {
        // Re-serialize frontmatter with YamlDotNet
        // Combine with markdown body
        // NOTE: Store original raw text to avoid YamlDotNet re-serialization artifacts
    }
}
```

### Pattern 4: Program.cs Entry Point
**What:** Host builder with stdio transport and all services registered
**When to use:** Application startup
**Example:**
```csharp
// Source: MCP C# SDK getting-started docs
var builder = Host.CreateApplicationBuilder(args);

// CRITICAL: All logging to stderr, never stdout
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Configuration binding
builder.Services.Configure<QdrantSkillsOptions>(
    builder.Configuration.GetSection("QdrantSkills"));

// Register services
builder.Services.AddQdrantSkillsInfrastructure();

// MCP server with stdio
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
```

### Pattern 5: Aspire AppHost Setup
**What:** Qdrant container with persistent lifetime for dev
**When to use:** Local development orchestration
**Example:**
```csharp
// Source: Aspire Qdrant integration docs
var builder = DistributedApplication.CreateBuilder(args);

var qdrant = builder.AddQdrant("qdrant")
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.QdrantSkillsMCP_Infrastructure>("server")
    .WithReference(qdrant)
    .WaitFor(qdrant);

builder.Build().Run();
```

### Anti-Patterns to Avoid
- **Writing anything to stdout except MCP JSON-RPC**: This is the #1 risk. Even a stray `Console.WriteLine` will corrupt the MCP transport. Use `LogToStandardErrorThreshold = LogLevel.Trace` to force all console logging to stderr.
- **Lossy YAML re-serialization**: Do not rely on YamlDotNet to reconstruct the original frontmatter. Store the raw skill content alongside parsed metadata. Return the raw content on retrieval.
- **Mutable singleton session tracker without concurrency control**: The session tracker must be connection-scoped or use `ConcurrentDictionary` if shared.
- **Blocking calls in tool methods**: All Qdrant and embedding operations are async. Never use `.Result` or `.Wait()`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| YAML parsing | Custom frontmatter parser | YamlDotNet Deserializer | Edge cases in YAML spec (multiline strings, special chars, anchors) |
| Embedding generation | Direct HTTP calls to OpenAI | Microsoft.Extensions.AI.OpenAI | Standard abstraction, enables multi-provider in Phase 2 |
| MCP protocol handling | JSON-RPC parsing/routing | ModelContextProtocol SDK | Protocol spec compliance, tool discovery, transport handling |
| Qdrant operations | REST API with HttpClient | Qdrant.Client (gRPC) | Typed client, connection pooling, proper error handling |
| Host lifecycle | Manual process management | Microsoft.Extensions.Hosting | Graceful shutdown, DI scoping, configuration binding |
| Container orchestration | Docker Compose for tests | Aspire.Hosting.Testing | Integrated with .NET host, port randomization, resource health checks |
| UUID generation from strings | Custom GUID algorithm | SHA256 + `new Guid(bytes)` | Deterministic, collision-resistant, standard approach |

**Key insight:** Every major component in this phase has a well-maintained library. The custom code should be thin glue between these libraries -- repository pattern over Qdrant, service wrappers over embeddings, and tool classes over the repository.

## Common Pitfalls

### Pitfall 1: Stdout Pollution Breaks MCP Transport
**What goes wrong:** Any non-JSON-RPC output on stdout causes the MCP client to fail with parse errors.
**Why it happens:** Default console logging writes to stdout. Debug output, exception dumps, or startup banners go to stdout.
**How to avoid:** Set `LogToStandardErrorThreshold = LogLevel.Trace` in console logger config. Never use `Console.WriteLine` -- use `ILogger` everywhere. Test by piping stdout through a JSON validator.
**Warning signs:** MCP client reports "invalid JSON" or silently disconnects.

### Pitfall 2: Qdrant Collection Not Ready on First Operation
**What goes wrong:** First skill add fails because collection doesn't exist yet.
**Why it happens:** Collection auto-creation is async; race condition between startup and first tool call.
**How to avoid:** Run `CollectionInitializer` during application startup (hosted service or lazy initialization with lock). Check if collection exists before creating. Use `CreateCollectionAsync` which is idempotent if collection already exists with same config.
**Warning signs:** Intermittent "collection not found" errors on first use.

### Pitfall 3: Embedding Dimension Mismatch
**What goes wrong:** Upsert fails because vector dimensions don't match collection config.
**Why it happens:** text-embedding-3-small produces 1536-dimensional vectors; text-embedding-3-large produces 3072. If collection was created with one model and queries use another, dimensions mismatch.
**How to avoid:** Store the expected dimensions in configuration. Validate embedding output dimensions against collection config on startup. Use a single embedding model configuration per collection.
**Warning signs:** Qdrant returns "wrong vector dimension" errors.

### Pitfall 4: YamlDotNet Re-Serialization Changes Content
**What goes wrong:** Round-tripping a skill through YamlDotNet changes whitespace, quoting style, or ordering.
**Why it happens:** YamlDotNet's serializer makes its own formatting choices that may differ from the original.
**How to avoid:** Store the ORIGINAL raw skill content (full markdown with frontmatter) in the Qdrant payload. Parse it for metadata extraction but return the stored raw content on retrieval. Only use YamlDotNet for reading, not writing back.
**Warning signs:** Diffs in skill content after add-then-load cycle.

### Pitfall 5: Aspire Qdrant Container Slow Start
**What goes wrong:** Integration tests fail intermittently because Qdrant isn't ready.
**Why it happens:** Qdrant container can take several seconds to start. Research flagged a known issue (GitHub #5768) with missing health checks in the Aspire Qdrant integration.
**How to avoid:** Use `.WaitFor(qdrant)` in AppHost. In integration tests, use `ResourceNotifications.WaitForResourceHealthyAsync()` or implement a custom readiness probe that checks the Qdrant health endpoint before running tests.
**Warning signs:** Tests pass individually but fail in parallel or on CI.

### Pitfall 6: Session Tracking Scope with Stdio Transport
**What goes wrong:** Session tracker shares state across what should be independent connections.
**Why it happens:** With stdio transport, there's typically one connection per process. But the tracker needs to be scoped correctly if the SDK adds session support.
**How to avoid:** For Phase 1 (stdio only), a singleton `InMemorySessionTracker` is fine since there's one connection per process. Design the `ISessionTracker` interface to accept an optional session ID parameter so Phase 2 can add connection-scoped tracking.
**Warning signs:** Skills marked as "already loaded" when they shouldn't be.

## Code Examples

### Qdrant Collection Auto-Creation
```csharp
// Source: Qdrant.Client docs + Qdrant REST API reference
public class CollectionInitializer
{
    private readonly QdrantClient _client;
    private readonly QdrantSkillsOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized;

    public async Task EnsureCollectionAsync(CancellationToken ct = default)
    {
        if (_initialized) return;
        await _lock.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            var collections = await _client.ListCollectionsAsync(ct);
            if (!collections.Any(c => c == _options.CollectionName))
            {
                await _client.CreateCollectionAsync(
                    _options.CollectionName,
                    new VectorParams
                    {
                        Size = (ulong)_options.VectorDimensions, // e.g., 1536 for text-embedding-3-small
                        Distance = Distance.Cosine
                    },
                    cancellationToken: ct);

                // Create payload indexes for efficient filtering
                await _client.CreatePayloadIndexAsync(
                    _options.CollectionName, "name",
                    PayloadSchemaType.Keyword, cancellationToken: ct);
                await _client.CreatePayloadIndexAsync(
                    _options.CollectionName, "archived",
                    PayloadSchemaType.Bool, cancellationToken: ct);
                await _client.CreatePayloadIndexAsync(
                    _options.CollectionName, "updated_at",
                    PayloadSchemaType.Datetime, cancellationToken: ct);
            }
            _initialized = true;
        }
        finally { _lock.Release(); }
    }
}
```

### OpenAI Embedding Service
```csharp
// Source: Microsoft.Extensions.AI docs
public class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

    public OpenAiEmbeddingService(IEmbeddingGenerator<string, Embedding<float>> generator)
    {
        _generator = generator;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var result = await _generator.GenerateAsync(text, cancellationToken: ct);
        return result[0].Vector.ToArray();
    }
}

// DI registration:
// services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
//     new OpenAIClient(apiKey).AsEmbeddingGenerator(modelId: "text-embedding-3-small"));
```

### Qdrant Upsert with Payload
```csharp
// Source: Qdrant.NET SDK README
public async Task AddSkillAsync(Skill skill, float[] embedding, CancellationToken ct)
{
    var pointId = GeneratePointId(skill.Name);
    var point = new PointStruct
    {
        Id = pointId,
        Vectors = embedding,
        Payload =
        {
            ["name"] = skill.Name,
            ["description"] = skill.Description ?? "",
            ["tags"] = skill.Tags ?? Array.Empty<string>(),
            ["raw_content"] = skill.RawContent,  // Full original markdown
            ["archived"] = false,
            ["updated_at"] = DateTimeOffset.UtcNow.ToString("o"),
        }
    };
    await _client.UpsertAsync(_options.CollectionName, new[] { point }, cancellationToken: ct);
}
```

### Aspire Integration Test
```csharp
// Source: Aspire testing docs
public class SkillCrudIntegrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task AddAndRetrieveSkill_RoundTripsLosslessly()
    {
        var ct = CancellationToken.None;
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.QdrantSkillsMCP_AppHost>(ct);

        appHost.Services.AddLogging(l => l.SetMinimumLevel(LogLevel.Debug));

        await using var app = await appHost.BuildAsync(ct).WaitAsync(Timeout, ct);
        await app.StartAsync(ct).WaitAsync(Timeout, ct);

        // Wait for Qdrant to be healthy
        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("qdrant", ct)
            .WaitAsync(Timeout, ct);

        // Get QdrantClient from DI or create from connection string
        // Execute test scenario...
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Custom JSON-RPC MCP impl | ModelContextProtocol C# SDK | Early 2025 | Official SDK with attribute-based tools |
| VSTest runner for xunit | XUnit v3 with MTP | Late 2025 | Native MTP support, no VSTest shims needed |
| Docker Compose for test infra | Aspire.Hosting.Testing | 2024-2025 | Integrated container lifecycle in test harness |
| `ITextEmbeddingGenerationService` (Semantic Kernel) | `IEmbeddingGenerator<string, Embedding<float>>` (M.E.AI) | 2025 | Microsoft's standard abstraction, not tied to SK |
| Manual OpenAI HTTP calls | `OpenAIClient.AsEmbeddingGenerator()` | 2025 | Extension method bridges OpenAI SDK to M.E.AI interface |

**Deprecated/outdated:**
- `Microsoft.SemanticKernel` embedding interfaces: replaced by `Microsoft.Extensions.AI.Abstractions`
- xunit v2 with VSTest runner: replaced by xunit v3 with MTP
- `ModelContextProtocol` preview packages (0.x): replaced by stable 1.1.0

## Open Questions

1. **MCP Session ID Availability in Stdio Transport**
   - What we know: HTTP transport exposes `Mcp-Session-Id` header. The SDK has `IMcpServer` injectable into tool methods.
   - What's unclear: Whether stdio transport has any session/connection identifier exposed on `IMcpServer`. Stdio is typically one connection per process.
   - Recommendation: For Phase 1, treat the process lifetime as the session. Design `ISessionTracker` to accept an optional session ID so Phase 2 can add explicit session support. Check `IMcpServer` properties at implementation time.

2. **Aspire Qdrant Health Check Completeness**
   - What we know: CONTEXT.md flagged GitHub #5768 about missing health checks. Aspire.Hosting.Qdrant v13.1.0+ exists.
   - What's unclear: Whether the latest Aspire Qdrant package includes proper health probes.
   - Recommendation: Implement a custom readiness check in integration tests that calls Qdrant's health endpoint directly. Use `.WaitFor()` in AppHost and add a fallback delay if health check is missing.

3. **Qdrant PayloadSchemaType for Bool and DateTime**
   - What we know: Qdrant supports keyword, integer, float, geo, text, bool, datetime, uuid index types via REST API. The .NET client exposes `PayloadSchemaType` enum.
   - What's unclear: Whether `PayloadSchemaType.Bool` and `PayloadSchemaType.Datetime` are available in Qdrant.Client v1.17.0 specifically.
   - Recommendation: Verify at implementation time. If not available, use keyword index on string representation as fallback.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.0+ with MTP |
| Config file | none -- Wave 0 |
| Quick run command | `dotnet test tests/QdrantSkillsMCP.UnitTests --no-build` |
| Full suite command | `dotnet test --no-build` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MCP-01 | Server starts with stdio transport | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "ServerStartup" --no-build -x` | Wave 0 |
| MCP-02 | Tools discoverable by MCP client | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "ToolDiscovery" --no-build -x` | Wave 0 |
| QDR-01 | Connects to configurable Qdrant | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "QdrantConnection" --no-build -x` | Wave 0 |
| QDR-02 | Configurable collection name | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "CollectionName" --no-build -x` | Wave 0 |
| QDR-03 | Qdrant API key support | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "ApiKeyConfig" --no-build -x` | Wave 0 |
| QDR-04 | Auto-creates collection on first use | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "CollectionAutoCreate" --no-build -x` | Wave 0 |
| CRUD-01 | add-skill persists to Qdrant | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "AddSkill" --no-build -x` | Wave 0 |
| CRUD-02 | update-skill re-embeds | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "UpdateSkill" --no-build -x` | Wave 0 |
| CRUD-03 | delete-skill removes permanently | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "DeleteSkill" --no-build -x` | Wave 0 |
| CRUD-04 | archive-skill soft-hides | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "ArchiveSkill" --no-build -x` | Wave 0 |
| CRUD-05 | YAML round-trip lossless | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "YamlRoundTrip" --no-build -x` | Wave 0 |
| SRCH-01 | Semantic search returns ranked results | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "SemanticSearch" --no-build -x` | Wave 0 |
| SRCH-02 | Temperature controls threshold | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "SearchTemperature" --no-build -x` | Wave 0 |
| SRCH-03 | max-results limits output | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "SearchMaxResults" --no-build -x` | Wave 0 |
| SRCH-04 | load-skill by name | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "LoadSkill" --no-build -x` | Wave 0 |
| SRCH-05 | load-skill returns current version | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "LoadSkillReload" --no-build -x` | Wave 0 |
| SRCH-06 | list-skills inventory | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "ListSkills" --no-build -x` | Wave 0 |
| EMB-01 | IEmbeddingGenerator abstraction | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "EmbeddingAbstraction" --no-build -x` | Wave 0 |
| EMB-02 | OpenAI embedding provider | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "OpenAiEmbedding" --no-build -x` | Wave 0 |
| DIST-02 | Aspire AppHost starts Qdrant | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "AspireQdrant" --no-build -x` | Wave 0 |
| DIST-03 | XUnit v3 MTP tests pass | smoke | `dotnet test --no-build` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/QdrantSkillsMCP.UnitTests --no-build`
- **Per wave merge:** `dotnet test --no-build`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `QdrantSkillsMCP.sln` -- solution file
- [ ] `src/QdrantSkillsMCP.Core/QdrantSkillsMCP.Core.csproj` -- project file
- [ ] `src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj` -- project file with all NuGet refs
- [ ] `src/QdrantSkillsMCP.AppHost/QdrantSkillsMCP.AppHost.csproj` -- Aspire AppHost project
- [ ] `tests/QdrantSkillsMCP.UnitTests/QdrantSkillsMCP.UnitTests.csproj` -- xunit.v3 + MTP config
- [ ] `tests/QdrantSkillsMCP.IntegrationTests/QdrantSkillsMCP.IntegrationTests.csproj` -- Aspire.Hosting.Testing + xunit.v3
- [ ] Test infrastructure: all test files listed above

## Sources

### Primary (HIGH confidence)
- [ModelContextProtocol C# SDK - Getting Started](https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html) - stdio server setup, tool attributes, DI patterns
- [ModelContextProtocol NuGet 1.1.0](https://www.nuget.org/packages/ModelContextProtocol/) - current stable version
- [Qdrant .NET SDK README](https://github.com/qdrant/qdrant-dotnet/blob/main/README.md) - client API, collection creation, upsert, search
- [Qdrant.Client NuGet 1.17.0](https://www.nuget.org/packages/Qdrant.Client) - current version
- [Microsoft.Extensions.AI docs](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai) - IEmbeddingGenerator interface, OpenAI integration
- [Microsoft.Extensions.AI.OpenAI NuGet 10.4.1](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI/) - OpenAI provider
- [Aspire Qdrant integration](https://aspire.dev/integrations/databases/qdrant/qdrant-get-started/) - AddQdrant, AddQdrantClient setup
- [Aspire Testing](https://aspire.dev/testing/write-your-first-test/) - DistributedApplicationTestingBuilder patterns
- [YamlDotNet NuGet 16.3.0](https://www.nuget.org/packages/YamlDotNet) - current version
- [XUnit v3 MTP docs](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform) - xunit.v3 with MTP setup

### Secondary (MEDIUM confidence)
- [DeepWiki MCP C# SDK Tools](https://deepwiki.com/modelcontextprotocol/csharp-sdk/2.1-server-tools) - DI injection patterns for tool classes
- [MCP C# SDK McpServerToolAttribute API](https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.Server.McpServerToolAttribute.html) - full attribute properties
- [.NET Blog MCP quickstart](https://devblogs.microsoft.com/dotnet/mcp-server-dotnet-nuget-quickstart/) - NuGet packaging guidance

### Tertiary (LOW confidence)
- Aspire Qdrant health check gap (GitHub #5768) - referenced in CONTEXT.md research, needs verification at implementation
- Qdrant.Client PayloadSchemaType.Bool/Datetime availability - needs runtime verification

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - all packages verified on NuGet with current versions
- Architecture: HIGH - patterns from official SDK docs and Microsoft examples
- Pitfalls: HIGH - stdout pollution and YAML round-trip are well-documented issues; Aspire health check gap flagged from prior research
- Code examples: MEDIUM - adapted from official docs; exact API signatures need verification against v1.17.0/v1.1.0

**Research date:** 2026-03-25
**Valid until:** 2026-04-25 (stable ecosystem, 30-day window)
