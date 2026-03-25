---
phase: 02-search-intelligence-and-embedding-providers
plan: 02
subsystem: api
tags: [onnx, ollama, azure-openai, embedding, semantic-kernel, aspire, di]

# Dependency graph
requires:
  - phase: 01-core-mcp-server
    provides: IEmbeddingService, OpenAiEmbeddingService, IEmbeddingGenerator registration, ServiceRegistration
  - phase: 02-01
    provides: EmbeddingProviderType enum, QdrantSkillsOptions with provider fields
provides:
  - OnnxEmbeddingService with BertOnnxTextEmbeddingGenerationService bridge
  - OllamaEmbeddingService with OllamaApiClient as IEmbeddingGenerator
  - AzureOpenAiEmbeddingService with AzureOpenAIClient bridge
  - OnnxModelResolver with three-tier model discovery (config/NuGet/auto-download)
  - Provider selection switch in ServiceRegistration
  - Aspire AppHost conditional Ollama container
affects: [02-03]

# Tech tracking
tech-stack:
  added: [Microsoft.SemanticKernel.Connectors.Onnx 1.74.0-alpha, OllamaSharp 5.4.25, Azure.AI.OpenAI 2.1.0, CommunityToolkit.Aspire.Hosting.Ollama 13.1.1]
  patterns: [provider-selection-switch, onnx-model-resolution, sk-to-meai-bridge, conditional-aspire-container]

key-files:
  created:
    - src/QdrantSkillsMCP.Infrastructure/Embedding/OnnxModelResolver.cs
    - src/QdrantSkillsMCP.Infrastructure/Embedding/OnnxEmbeddingService.cs
    - src/QdrantSkillsMCP.Infrastructure/Embedding/OllamaEmbeddingService.cs
    - src/QdrantSkillsMCP.Infrastructure/Embedding/AzureOpenAiEmbeddingService.cs
    - tests/QdrantSkillsMCP.UnitTests/Embedding/OnnxEmbeddingServiceTests.cs
    - tests/QdrantSkillsMCP.UnitTests/Embedding/OllamaEmbeddingServiceTests.cs
    - tests/QdrantSkillsMCP.UnitTests/Embedding/AzureOpenAiEmbeddingServiceTests.cs
  modified:
    - src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj
    - src/QdrantSkillsMCP.Infrastructure/ServiceRegistration.cs
    - src/QdrantSkillsMCP.AppHost/QdrantSkillsMCP.AppHost.csproj
    - src/QdrantSkillsMCP.AppHost/Program.cs

key-decisions:
  - "Used BertOnnxTextEmbeddingGenerationService.Create() with AsEmbeddingGenerator() bridge from SK to M.E.AI IEmbeddingGenerator"
  - "OllamaApiClient natively implements IEmbeddingGenerator -- no bridge needed"
  - "PostConfigure overrides VectorDimensions to 384 for ONNX when still at OpenAI default 1536"
  - "Provider selection reads config at registration time via IConfiguration, not IOptions"

patterns-established:
  - "Provider registration pattern: switch on enum, each case registers IEmbeddingGenerator + IEmbeddingService pair"
  - "ONNX model resolution: config path > NuGet companion > auto-download cache"
  - "Conditional Aspire container: read config to decide whether to add Ollama"

requirements-completed: [EMB-03, EMB-04, EMB-05]

# Metrics
duration: 8min
completed: 2026-03-25
---

# Phase 2 Plan 2: Embedding Providers Summary

**Three embedding providers (LocalONNX, Ollama, Azure OpenAI) with provider selection switch, ONNX model auto-download, and conditional Aspire Ollama container**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-25T23:03:33Z
- **Completed:** 2026-03-25T23:11:33Z
- **Tasks:** 2
- **Files modified:** 11

## Accomplishments
- Three new IEmbeddingService implementations (ONNX, Ollama, Azure OpenAI) following the same IEmbeddingGenerator wrapper pattern
- OnnxModelResolver with three-tier model discovery: explicit config path, companion NuGet scan, auto-download from HuggingFace
- ServiceRegistration provider switch on EmbeddingProviderType with defaults and validation
- Aspire AppHost conditional Ollama container with model pull and persistent data volume
- 19 new unit tests covering all three providers (84 total unit tests passing)

## Task Commits

Each task was committed atomically:

1. **Task 1: NuGet packages, ONNX model resolver, and three embedding services** - `c04a4b2` (feat)
2. **Task 2: ServiceRegistration provider switch, AppHost Ollama, and unit tests** - `796a177` (feat)

## Files Created/Modified
- `src/QdrantSkillsMCP.Infrastructure/Embedding/OnnxModelResolver.cs` - Three-tier ONNX model file resolution with auto-download
- `src/QdrantSkillsMCP.Infrastructure/Embedding/OnnxEmbeddingService.cs` - Local ONNX embedding via SK BertOnnx bridge
- `src/QdrantSkillsMCP.Infrastructure/Embedding/OllamaEmbeddingService.cs` - Ollama embedding via OllamaSharp client
- `src/QdrantSkillsMCP.Infrastructure/Embedding/AzureOpenAiEmbeddingService.cs` - Azure OpenAI embedding via Azure.AI.OpenAI
- `src/QdrantSkillsMCP.Infrastructure/ServiceRegistration.cs` - Provider selection switch with defaults and validation
- `src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj` - Added ONNX, OllamaSharp, Azure.AI.OpenAI packages
- `src/QdrantSkillsMCP.AppHost/Program.cs` - Conditional Ollama container when provider is Ollama
- `src/QdrantSkillsMCP.AppHost/QdrantSkillsMCP.AppHost.csproj` - Added CommunityToolkit.Aspire.Hosting.Ollama
- `tests/QdrantSkillsMCP.UnitTests/Embedding/OnnxEmbeddingServiceTests.cs` - 7 tests: vector delegation, 384 default dims, custom dims, null/empty input
- `tests/QdrantSkillsMCP.UnitTests/Embedding/OllamaEmbeddingServiceTests.cs` - 6 tests: vector delegation, configured dims, cancellation, null/empty input
- `tests/QdrantSkillsMCP.UnitTests/Embedding/AzureOpenAiEmbeddingServiceTests.cs` - 6 tests: vector delegation, configured dims, cancellation, null/empty input

## Decisions Made
- Used `BertOnnxTextEmbeddingGenerationService.Create()` (marked obsolete) with `AsEmbeddingGenerator()` bridge rather than the newer `AddBertOnnxEmbeddingGenerator` DI extension, because we needed deferred path resolution from IOptions at service creation time
- `OllamaApiClient` directly implements `IEmbeddingGenerator<string, Embedding<float>>`, no bridge needed
- Provider selection reads from `IConfiguration` directly at registration time (before IOptions is bound) to determine which services to register
- `PostConfigure<QdrantSkillsOptions>` auto-corrects VectorDimensions from 1536 (OpenAI default) to 384 for ONNX, preventing vector dimension mismatch

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed test assertion for null input validation**
- **Found during:** Task 2 (unit tests)
- **Issue:** `ArgumentException.ThrowIfNullOrWhiteSpace(null)` throws `ArgumentNullException` (derived), but `Assert.ThrowsAsync<ArgumentException>` expects exact type match in xunit.v3
- **Fix:** Changed to `Assert.ThrowsAnyAsync<ArgumentException>` which accepts derived exception types
- **Files modified:** All three new test files
- **Committed in:** 796a177 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Minor test assertion fix. No scope creep.

## Issues Encountered
- `BertOnnxTextEmbeddingGenerationService` does not directly implement `IEmbeddingGenerator` in SK 1.74 -- resolved by using the `AsEmbeddingGenerator()` bridge extension from `Microsoft.SemanticKernel.Embeddings`

## User Setup Required

None - no external service configuration required. ONNX provider auto-downloads model on first use.

## Next Phase Readiness
- All four embedding providers (OpenAI, ONNX, Ollama, Azure OpenAI) fully registered and testable
- Ready for Plan 03 (search quality / hybrid search improvements)
- 84 unit tests passing, full solution builds cleanly

---
*Phase: 02-search-intelligence-and-embedding-providers*
*Completed: 2026-03-25*
