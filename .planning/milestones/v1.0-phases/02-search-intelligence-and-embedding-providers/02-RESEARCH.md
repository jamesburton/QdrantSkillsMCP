# Phase 2: Search Intelligence and Embedding Providers - Research

**Researched:** 2026-03-25
**Domain:** Embedding provider abstraction, ONNX local inference, Ollama integration, Azure OpenAI, session-aware search
**Confidence:** HIGH

## Summary

Phase 2 adds three new embedding providers (LocalONNX, Ollama, Azure OpenAI) alongside the existing OpenAI provider, introduces output mode switching (full/names/summaries) on search and list tools, extends session tracking to support keyed sessions, and adds startup dimension validation. The codebase already has a clean `IEmbeddingService` abstraction and `IEmbeddingGenerator<string, Embedding<float>>` from Microsoft.Extensions.AI, so new providers follow the established pattern. The ONNX provider uses `Microsoft.SemanticKernel.Connectors.Onnx` (BertOnnxTextEmbeddingGenerationService) which provides an `.AsEmbeddingGenerator()` bridge to the Microsoft.Extensions.AI interface. Ollama uses `OllamaSharp` which natively implements `IEmbeddingGenerator`. Azure OpenAI reuses the existing `Microsoft.Extensions.AI.OpenAI` package with `AzureOpenAIClient` instead of `OpenAIClient`. Aspire Ollama hosting uses `CommunityToolkit.Aspire.Hosting.Ollama`. Dimension validation queries Qdrant's `GetCollectionInfoAsync` to compare collection vector size against the configured provider's dimensions.

**Primary recommendation:** Implement provider selection as a switch in `ServiceRegistration` that registers the appropriate `IEmbeddingGenerator<string, Embedding<float>>` singleton based on `QdrantSkillsOptions.EmbeddingProvider`. All providers funnel through the same `IEmbeddingService` wrapper pattern established by `OpenAiEmbeddingService` (or a shared generic implementation).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Single `EmbeddingProvider` config key in `QdrantSkillsOptions`: `"LocalONNX"`, `"OpenAI"`, `"Ollama"`, `"AzureOpenAI"`
- **Default behavior (no provider set):** Default to LocalONNX with a warning recommending the user explicitly set `"EmbeddingProvider": "LocalONNX"` (or `QDRANT_SKILLS__EmbeddingProvider=LocalONNX`) to silence the warning
- Provider wiring happens in `ServiceRegistration` based on the config key value
- ONNX provider uses Microsoft.Extensions.AI ONNX support for loading model and providing `IEmbeddingGenerator` interface
- **ONNX Model sourcing (three-tier):** 1. Config path to custom `.onnx` file, 2. Companion NuGet package, 3. Auto-download from HuggingFace on first use (with `DisableAutoDownload` opt-out)
- Default model: all-MiniLM-L6-v2 (or equivalent small, high-quality model)
- Ollama inside Aspire: always spin up container via Aspire integration. Outside Aspire: default to `http://localhost:11434` with warning if no `EmbeddingUrl` set
- Azure OpenAI: standard config (endpoint URL + API key + deployment name), uses `Microsoft.Extensions.AI` Azure OpenAI integration
- Replace `includeContent` boolean with `outputMode` enum: `"full"` (default), `"names"`, `"summaries"`
- Session tracking: only `"full"` mode marks skills as loaded
- **Dimension mismatch handling:** Hard fail on startup with clear guidance. Resolution flags: `--rename-mismatched`, `--model-suffix`, `--replace-mismatched`
- **Embedding output validation:** Test embedding stored in Qdrant with `_test: true` payload, excluded from searches. Configurable key/input, skippable with `--skip-embedding-output-validation`
- **Session ID override:** Optional `sessionId` parameter on `search-skills` and `load-skill`. `InMemorySessionTracker` evolves to `ConcurrentDictionary<string, HashSet<string>>`
- New `reset-session` MCP tool

### Claude's Discretion
- Exact ONNX default model selection (all-MiniLM-L6-v2 or similar)
- Azure OpenAI config key naming (follow Azure SDK conventions)
- Internal structure of the provider factory/selector pattern
- OllamaSharp vs Microsoft.Extensions.AI.Ollama package choice
- Exact test embedding exclusion filter implementation
- Aspire Ollama integration details (container image, port mapping)

### Deferred Ideas (OUT OF SCOPE)
- None
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SRCH-07 | `--names` option returns skill names only | OutputMode enum replaces includeContent; names mode returns string array |
| SRCH-08 | `--summaries` option returns name + short summary | OutputMode summaries mode returns name + description without raw_content |
| SRCH-09 | Search results include `ALREADY LOADED SKILLS` listing | Already partially implemented; needs session ID keying |
| SRCH-10 | Session tracking with explicit session ID override | InMemorySessionTracker evolves to keyed ConcurrentDictionary |
| EMB-03 | Local ONNX embedding provider | BertOnnxTextEmbeddingGenerationService from SemanticKernel.Connectors.Onnx + AsEmbeddingGenerator() bridge |
| EMB-04 | Ollama embedding provider | OllamaSharp OllamaApiClient implements IEmbeddingGenerator natively |
| EMB-05 | Azure OpenAI embedding provider | AzureOpenAIClient from Azure.AI.OpenAI + AsEmbeddingGenerator() from Microsoft.Extensions.AI.OpenAI |
| EMB-06 | Embedding dimension validation on startup | IHostedService checks GetCollectionInfoAsync vector size vs provider dimensions |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.SemanticKernel.Connectors.Onnx | 1.17.1-alpha | ONNX local embedding via BertOnnxTextEmbeddingGenerationService | Official Microsoft ONNX connector; provides AsEmbeddingGenerator() bridge to M.E.AI |
| OllamaSharp | 5.4.16 | Ollama embedding provider | Natively implements IEmbeddingGenerator; recommended replacement for deprecated M.E.AI.Ollama |
| Azure.AI.OpenAI | 2.* | Azure OpenAI client | Official Azure SDK; AzureOpenAIClient works with existing M.E.AI.OpenAI AsEmbeddingGenerator() |
| Microsoft.Extensions.AI.OpenAI | 10.* | Already in project; provides AsEmbeddingGenerator() for both OpenAI and Azure OpenAI clients | Already used in Phase 1 |
| CommunityToolkit.Aspire.Hosting.Ollama | 9.2.1 (stable) | Aspire AppHost Ollama container hosting | Official Aspire community toolkit; AddOllama + AddModel API |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| CommunityToolkit.Aspire.OllamaSharp | 9.2.1 | Aspire service defaults for OllamaSharp DI | Only in Aspire-hosted scenarios for automatic connection wiring |
| Microsoft.ML.OnnxRuntime | (transitive) | ONNX runtime engine | Pulled in transitively by SemanticKernel.Connectors.Onnx |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| SemanticKernel.Connectors.Onnx | SmartComponents.LocalEmbeddings | SmartComponents is simpler but uses its own interface; SK connector provides direct AsEmbeddingGenerator() bridge and more mature API |
| SemanticKernel.Connectors.Onnx | AllMiniLmL6V2Sharp | Dedicated to one model only; no M.E.AI integration |
| OllamaSharp | Microsoft.Extensions.AI.Ollama | M.E.AI.Ollama is **deprecated** with no further updates; OllamaSharp is the recommended replacement |

**Installation (Infrastructure.csproj):**
```bash
dotnet add src/QdrantSkillsMCP.Infrastructure package Microsoft.SemanticKernel.Connectors.Onnx --prerelease
dotnet add src/QdrantSkillsMCP.Infrastructure package OllamaSharp
dotnet add src/QdrantSkillsMCP.Infrastructure package Azure.AI.OpenAI
```

**Installation (AppHost.csproj):**
```bash
dotnet add src/QdrantSkillsMCP.AppHost package CommunityToolkit.Aspire.Hosting.Ollama
```

## Architecture Patterns

### Recommended Project Structure
```
src/QdrantSkillsMCP.Infrastructure/
  Configuration/
    QdrantSkillsOptions.cs          # Add EmbeddingProvider, EmbeddingUrl, Azure fields, ONNX fields
    EmbeddingProviderType.cs         # Enum: LocalONNX, OpenAI, Ollama, AzureOpenAI
  Embedding/
    OpenAiEmbeddingService.cs        # Existing (unchanged or refactored to shared base)
    OnnxEmbeddingService.cs          # NEW: wraps BertOnnxTextEmbeddingGenerationService
    OllamaEmbeddingService.cs        # NEW: wraps OllamaSharp OllamaApiClient
    AzureOpenAiEmbeddingService.cs   # NEW: wraps AzureOpenAIClient
  Qdrant/
    CollectionInitializer.cs         # Existing
    DimensionValidator.cs            # NEW: IHostedService for startup dimension check
  Session/
    InMemorySessionTracker.cs        # MODIFIED: keyed sessions
  Tools/
    SkillSearchTools.cs              # MODIFIED: outputMode parameter, sessionId parameter
    SessionTools.cs                  # NEW: reset-session MCP tool
  ServiceRegistration.cs             # MODIFIED: provider selection switch
```

### Pattern 1: Provider Selection in ServiceRegistration
**What:** Switch on `EmbeddingProviderType` enum to register the correct `IEmbeddingGenerator` and `IEmbeddingService`
**When to use:** At DI registration time in `AddQdrantSkillsInfrastructure`
**Example:**
```csharp
// In ServiceRegistration.cs
var provider = options.EmbeddingProvider ?? EmbeddingProviderType.LocalONNX;

if (options.EmbeddingProvider is null)
{
    // Log warning: "No embedding provider configured. Defaulting to LocalONNX.
    // Set QdrantSkills:EmbeddingProvider or QDRANT_SKILLS__EmbeddingProvider to silence this warning."
}

switch (provider)
{
    case EmbeddingProviderType.OpenAI:
        // existing OpenAI registration
        break;
    case EmbeddingProviderType.LocalONNX:
        // Register BertOnnxTextEmbeddingGenerationService + AsEmbeddingGenerator() bridge
        break;
    case EmbeddingProviderType.Ollama:
        // Register OllamaApiClient as IEmbeddingGenerator
        break;
    case EmbeddingProviderType.AzureOpenAI:
        // Register AzureOpenAIClient + AsEmbeddingGenerator()
        break;
}
```

### Pattern 2: ONNX Provider with Three-Tier Model Resolution
**What:** Resolve ONNX model file path through config override, companion NuGet, or auto-download
**When to use:** During ONNX embedding service initialization
**Example:**
```csharp
// Model resolution order:
// 1. Explicit config path: options.OnnxModelPath
// 2. Well-known companion package location (scan for model.onnx in NuGet package content)
// 3. Auto-download from HuggingFace to local cache directory
string modelPath = ResolveOnnxModelPath(options);
string vocabPath = ResolveOnnxVocabPath(options);

var service = BertOnnxTextEmbeddingGenerationService.Create(modelPath, vocabPath);
var generator = service.AsEmbeddingGenerator<string, Embedding<float>>();
```

### Pattern 3: Startup Dimension Validation (IHostedService)
**What:** Validate embedding dimensions before MCP tools become available
**When to use:** Application startup, after DI is built
**Example:**
```csharp
public class DimensionValidator : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        // 1. Check if collection exists
        // 2. If exists: GetCollectionInfoAsync -> compare info.Config.Params.Vectors.Size
        //    with embeddingService.Dimensions
        // 3. If mismatch: throw with clear guidance message
        // 4. Generate test embedding to verify provider output dimensions
        // 5. Store test point with _test: true payload flag
    }
}
```

### Pattern 4: Keyed Session Tracker
**What:** ConcurrentDictionary keyed by session ID, with default session for backward compatibility
**When to use:** All session tracking operations
**Example:**
```csharp
public sealed class InMemorySessionTracker : ISessionTracker
{
    private const string DefaultSessionId = "__default__";
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _sessions = new();

    public void MarkLoaded(string skillName, string? sessionId = null)
    {
        var key = sessionId ?? DefaultSessionId;
        var session = _sessions.GetOrAdd(key, _ => new(StringComparer.OrdinalIgnoreCase));
        session.TryAdd(skillName, 0);
    }
}
```

### Anti-Patterns to Avoid
- **Registering all providers simultaneously:** Only register the configured provider's `IEmbeddingGenerator`. Do not register all four and select at runtime.
- **Lazy dimension validation:** The dimension check MUST run at startup (IHostedService.StartAsync), not on first search call. A mismatch discovered mid-session is much harder to recover from.
- **Shared mutable state in embedding services:** Each provider implementation should be stateless beyond its client instance. The `IEmbeddingService` implementations are registered as singletons.
- **Auto-downloading without user consent logging:** Always log clearly when auto-downloading the ONNX model from HuggingFace. Users must know external network calls are happening.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| ONNX model inference | Custom ONNX runtime wrapper | BertOnnxTextEmbeddingGenerationService from SemanticKernel.Connectors.Onnx | Handles tokenization, model loading, ONNX session management, vocab parsing |
| BERT tokenization | Custom tokenizer for all-MiniLM-L6-v2 | Built into SemanticKernel.Connectors.Onnx (requires vocab.txt) | BERT tokenization is non-trivial (WordPiece, special tokens, truncation) |
| Ollama API client | Raw HTTP calls to Ollama | OllamaSharp OllamaApiClient | Handles model pulling, streaming, endpoint discovery, IEmbeddingGenerator |
| Azure OpenAI authentication | Custom token/key handling | Azure.AI.OpenAI AzureOpenAIClient | Handles API versioning, managed identity, key rotation |
| IEmbeddingGenerator bridge | Adapter from SK to M.E.AI | .AsEmbeddingGenerator() extension method | One-liner; provided by SemanticKernel |
| HuggingFace model download | Custom HTTP downloader | HttpClient with standard HuggingFace API patterns | Keep it simple: download model.onnx + vocab.txt to a cache dir |

**Key insight:** The Microsoft.Extensions.AI `IEmbeddingGenerator<string, Embedding<float>>` interface is the unification point. OpenAI and Azure OpenAI get there via `.AsEmbeddingGenerator()` on their SDK clients. OllamaSharp implements it natively. SemanticKernel ONNX gets there via `.AsEmbeddingGenerator()` extension on `IEmbeddingGenerationService`.

## Common Pitfalls

### Pitfall 1: SemanticKernel.Connectors.Onnx is Alpha/Experimental
**What goes wrong:** Build warnings or pragma suppression needed for experimental APIs
**Why it happens:** The package is versioned as `-alpha` and APIs are marked with `[Experimental]` attributes
**How to avoid:** Add `<NoWarn>SKEXP0070</NoWarn>` (or the relevant experimental warning code) to the Infrastructure .csproj PropertyGroup. Document this is intentional.
**Warning signs:** CS warnings about experimental APIs during build

### Pitfall 2: ONNX Model File Discovery in NuGet Tool Context
**What goes wrong:** Model file not found when running as a `dotnet tool` vs development
**Why it happens:** NuGet tools run from a different directory than the project. Relative paths break.
**How to avoid:** Use `AppContext.BaseDirectory` for companion NuGet model resolution. For auto-download, use a well-known cache directory like `Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "QdrantSkillsMCP", "models")`.
**Warning signs:** FileNotFoundException for model.onnx in production but not dev

### Pitfall 3: Qdrant Collection Dimension Immutability
**What goes wrong:** Cannot change vector dimensions on an existing collection
**Why it happens:** Qdrant does not support altering vector size after collection creation
**How to avoid:** The startup dimension validator MUST check before any writes. The resolution flags (`--rename-mismatched`, `--replace-mismatched`, `--model-suffix`) provide escape hatches.
**Warning signs:** Qdrant errors on upsert with wrong-dimension vectors

### Pitfall 4: OllamaSharp Embedding Model Must Be Pulled First
**What goes wrong:** 404 or model-not-found error from Ollama when generating embeddings
**Why it happens:** Ollama requires models to be explicitly pulled before use
**How to avoid:** In Aspire mode, `AddModel("embeddings", "all-minilm:l6-v2")` handles this. In standalone mode, log a clear error: "Model 'X' not found. Run `ollama pull X` first."
**Warning signs:** OllamaSharp throwing on first embedding call

### Pitfall 5: Azure OpenAI Deployment Name vs Model Name
**What goes wrong:** Azure OpenAI requires a deployment name, not a model name
**Why it happens:** Azure OpenAI uses custom deployment names that may differ from the underlying model name
**How to avoid:** Config should have separate `AzureOpenAiDeployment` key (not reuse `EmbeddingModel`). Follow Azure SDK naming conventions.
**Warning signs:** 404 from Azure OpenAI endpoint

### Pitfall 6: VectorDimensions Default of 1536 Conflicts with ONNX Default
**What goes wrong:** Collection created with 1536 dims (OpenAI default) but ONNX provider produces 384 dims
**Why it happens:** `QdrantSkillsOptions.VectorDimensions` defaults to 1536 from Phase 1
**How to avoid:** When provider is LocalONNX, override VectorDimensions to match the model (384 for all-MiniLM-L6-v2). The dimension validator catches mismatches, but setting the correct default per-provider is better UX.
**Warning signs:** Dimension mismatch error on first run with LocalONNX

## Code Examples

### ONNX Provider Registration
```csharp
// Source: Microsoft.SemanticKernel.Connectors.Onnx official docs + AsEmbeddingGenerator extension
#pragma warning disable SKEXP0070 // Experimental API

var modelPath = ResolveOnnxModelPath(options);
var vocabPath = ResolveOnnxVocabPath(options);

var onnxService = BertOnnxTextEmbeddingGenerationService.Create(modelPath, vocabPath);
services.AddSingleton(onnxService);
services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
    onnxService.AsEmbeddingGenerator<string, Embedding<float>>());

#pragma warning restore SKEXP0070
```

### OllamaSharp Provider Registration
```csharp
// Source: OllamaSharp GitHub + Microsoft.Extensions.AI docs
var ollamaUri = new Uri(options.EmbeddingUrl ?? "http://localhost:11434");
var ollamaClient = new OllamaApiClient(ollamaUri, options.EmbeddingModel ?? "all-minilm:l6-v2");
// OllamaApiClient implements IEmbeddingGenerator<string, Embedding<float>> natively
services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(ollamaClient);
```

### Azure OpenAI Provider Registration
```csharp
// Source: Microsoft.Extensions.AI.OpenAI docs + Azure.AI.OpenAI
var azureClient = new AzureOpenAIClient(
    new Uri(options.AzureOpenAiEndpoint!),
    new System.ClientModel.ApiKeyCredential(options.AzureOpenAiApiKey!));
var generator = azureClient
    .GetEmbeddingClient(options.AzureOpenAiDeployment!)
    .AsIEmbeddingGenerator();
services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(generator);
```

### Aspire Ollama AppHost Configuration
```csharp
// Source: CommunityToolkit.Aspire.Hosting.Ollama blog post
var ollama = builder.AddOllama("ollama")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var embeddingModel = ollama.AddModel("embedding", "all-minilm:l6-v2");

builder.AddProject<Projects.QdrantSkillsMCP_Infrastructure>("server")
    .WithReference(qdrant)
    .WithReference(embeddingModel)
    .WaitFor(qdrant)
    .WaitFor(embeddingModel);
```

### Qdrant Dimension Check
```csharp
// Source: Qdrant .NET SDK docs
var collections = await client.ListCollectionsAsync(ct);
if (collections.Contains(options.CollectionName))
{
    var info = await client.GetCollectionInfoAsync(options.CollectionName, ct);
    var existingDims = (int)info.Config.Params.Vectors.Size;
    var providerDims = embeddingService.Dimensions;

    if (existingDims != providerDims)
    {
        throw new InvalidOperationException(
            $"Collection '{options.CollectionName}' has {existingDims}-dim vectors " +
            $"but provider '{options.EmbeddingProvider}' produces {providerDims}-dim. " +
            $"Use --rename-mismatched, --model-suffix, or --replace-mismatched to resolve.");
    }
}
```

### OutputMode Enum and Usage
```csharp
public enum OutputMode
{
    Full,       // Returns full skill content (name, description, tags, score, raw_content)
    Names,      // Returns skill names only (string array)
    Summaries   // Returns name + description per skill (no raw_content)
}

// In SkillSearchTools.SearchSkills:
[McpServerTool(Name = "search-skills", ReadOnly = true)]
public async Task<string> SearchSkills(
    string query,
    int maxResults = 5,
    float? temperature = null,
    string outputMode = "full",  // MCP tools use string params; parse to enum internally
    string? sessionId = null,
    CancellationToken ct = default)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Microsoft.Extensions.AI.Ollama | OllamaSharp (natively implements IEmbeddingGenerator) | 2025 (M.E.AI.Ollama deprecated) | Use OllamaSharp directly, not the deprecated M.E.AI wrapper |
| Custom ONNX wrappers | SemanticKernel.Connectors.Onnx + AsEmbeddingGenerator() | 2024-2025 | Standardized bridge from SK to M.E.AI interfaces |
| Separate OpenAI/Azure clients | Both use Microsoft.Extensions.AI.OpenAI AsIEmbeddingGenerator() | 2025 (M.E.AI GA) | Same package handles both; AzureOpenAIClient from Azure.AI.OpenAI |

**Deprecated/outdated:**
- `Microsoft.Extensions.AI.Ollama`: Deprecated, no further updates. Use OllamaSharp instead.
- `SmartComponents.LocalEmbeddings`: Now internally uses SemanticKernel ONNX. Use SK connector directly for M.E.AI compatibility.

## Open Questions

1. **all-MiniLM-L6-v2 ONNX model + vocab.txt exact HuggingFace source**
   - What we know: Model is 384-dim, available on HuggingFace at `onnx-models/all-MiniLM-L6-v2-onnx` and `Xenova/all-MiniLM-L6-v2`
   - What's unclear: Which repo provides the exact format (model.onnx + vocab.txt) that BertOnnxTextEmbeddingGenerationService expects
   - Recommendation: Use `onnx-models/all-MiniLM-L6-v2-onnx` from HuggingFace. Verify at implementation time that the model and vocab files are compatible with BertOnnxTextEmbeddingGenerationService.

2. **Companion NuGet model package feasibility**
   - What we know: The CONTEXT.md specifies a `QdrantSkillsMCP.Models.DefaultEmbedding` companion package containing the default ONNX model
   - What's unclear: Exact packaging mechanism for embedding an ONNX model (~25-90MB) in a NuGet package as content files
   - Recommendation: Implement tier 1 (config path) and tier 3 (auto-download) first. Companion NuGet package can be deferred to a follow-up if complex.

3. **SemanticKernel.Connectors.Onnx experimental pragma code**
   - What we know: The package is alpha and uses `[Experimental]` attributes
   - What's unclear: Exact pragma warning code (likely SKEXP0070 or similar)
   - Recommendation: Check at implementation time; add appropriate `<NoWarn>` to .csproj.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 + Aspire.Hosting.Testing 13.2.0 |
| Config file | Tests use Aspire AppHost builder (no separate config file) |
| Quick run command | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~Phase2" -x` |
| Full suite command | `dotnet test` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SRCH-07 | Names output mode returns names only | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~OutputMode" -x` | No - Wave 0 |
| SRCH-08 | Summaries output mode returns name+description | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~OutputMode" -x` | No - Wave 0 |
| SRCH-09 | Already loaded skills listed in search results | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "FullyQualifiedName~Session" -x` | Partial (existing search tests cover basic case) |
| SRCH-10 | Session ID override support | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~SessionTracker" -x` | Partial (existing InMemorySessionTrackerTests, needs keyed extension) |
| EMB-03 | ONNX embedding provider | unit + integration | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~Onnx" -x` | No - Wave 0 |
| EMB-04 | Ollama embedding provider | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~Ollama" -x` | No - Wave 0 |
| EMB-05 | Azure OpenAI embedding provider | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~AzureOpenAi" -x` | No - Wave 0 |
| EMB-06 | Dimension validation on startup | unit + integration | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~DimensionValidator" -x` | No - Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/QdrantSkillsMCP.UnitTests -x`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/QdrantSkillsMCP.UnitTests/Embedding/OnnxEmbeddingServiceTests.cs` -- covers EMB-03
- [ ] `tests/QdrantSkillsMCP.UnitTests/Embedding/OllamaEmbeddingServiceTests.cs` -- covers EMB-04
- [ ] `tests/QdrantSkillsMCP.UnitTests/Embedding/AzureOpenAiEmbeddingServiceTests.cs` -- covers EMB-05
- [ ] `tests/QdrantSkillsMCP.UnitTests/Qdrant/DimensionValidatorTests.cs` -- covers EMB-06
- [ ] `tests/QdrantSkillsMCP.UnitTests/Tools/OutputModeTests.cs` -- covers SRCH-07, SRCH-08
- [ ] `tests/QdrantSkillsMCP.UnitTests/Session/KeyedSessionTrackerTests.cs` -- covers SRCH-10
- [ ] `tests/QdrantSkillsMCP.IntegrationTests/SessionIdIntegrationTests.cs` -- covers SRCH-09, SRCH-10

## Sources

### Primary (HIGH confidence)
- [Microsoft.Extensions.AI.OpenAI 10.4.1 NuGet](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI/) - Confirmed AsIEmbeddingGenerator() for OpenAI and Azure OpenAI
- [BertOnnxTextEmbeddingGenerationService Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/api/microsoft.semantickernel.connectors.onnx.bertonnxtextembeddinggenerationservice?view=semantic-kernel-dotnet) - Confirmed Create/CreateAsync methods, AsEmbeddingGenerator extension, IDisposable
- [OllamaSharp 5.4.16 NuGet](https://www.nuget.org/packages/OllamaSharp) - Confirmed IEmbeddingGenerator native implementation
- [Qdrant.Client 1.17.0 NuGet](https://www.nuget.org/packages/Qdrant.Client) - GetCollectionInfoAsync for dimension checking
- [CommunityToolkit.Aspire.Hosting.Ollama NuGet](https://www.nuget.org/packages/CommunityToolkit.Aspire.Hosting.Ollama/) - AddOllama + AddModel API

### Secondary (MEDIUM confidence)
- [.NET Blog - Local AI Models with Aspire](https://devblogs.microsoft.com/dotnet/local-ai-models-with-dotnet-aspire/) - Verified Aspire Ollama integration patterns and code examples
- [Qdrant .NET SDK DeepWiki](https://deepwiki.com/qdrant/qdrant-dotnet/3.1-collection-management) - GetCollectionInfoAsync usage pattern verified
- [Microsoft.Extensions.AI.Ollama deprecated](https://www.nuget.org/packages/Microsoft.Extensions.AI.Ollama) - Confirmed deprecation notice on NuGet

### Tertiary (LOW confidence)
- [all-MiniLM-L6-v2-onnx HuggingFace](https://huggingface.co/onnx-models/all-MiniLM-L6-v2-onnx) - Model file structure needs verification at implementation time
- SemanticKernel.Connectors.Onnx experimental warning code (SKEXP0070) - needs verification at build time

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All packages verified on NuGet with current versions; APIs confirmed via official docs
- Architecture: HIGH - Provider pattern follows existing OpenAI implementation; M.E.AI interfaces are the unification layer
- Pitfalls: HIGH - Dimension mismatch, model discovery, and deprecated package issues are well-documented
- ONNX model sourcing: MEDIUM - HuggingFace model format compatibility with BertOnnxTextEmbeddingGenerationService needs runtime verification

**Research date:** 2026-03-25
**Valid until:** 2026-04-25 (packages are stable/GA except SemanticKernel.Connectors.Onnx which is alpha)
