# Stack Research

**Domain:** .NET MCP Server with Qdrant Vector Storage
**Researched:** 2026-03-25
**Confidence:** HIGH

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| .NET 10 (LTS) | 10.0 | Runtime and SDK | GA since Nov 2025. LTS with 3 years of support. Required for `dnx` tool runner, latest Aspire compatibility, and C# 14 features. |
| ModelContextProtocol | 1.1.0 | MCP server SDK | Official C# SDK maintained by Microsoft + Anthropic. 5.2M downloads. Provides `AddMcpServer()`, `WithStdioServerTransport()`, `WithToolsFromAssembly()` — exactly the pattern this project needs. Use the main package (not `.Core` or `.AspNetCore`) since we want DI + stdio, not HTTP. |
| Qdrant.Client | 1.17.0 | Vector database client | Official .NET client from Qdrant. gRPC-based, targets .NET 6+/netstandard2.0. Directly registered by Aspire integration. |
| Aspire | 13.x (latest 13.2.0) | Local dev orchestration and testing | Aspire rebranded/re-versioned in Nov 2025 (jumped from 9.x to 13). Supports .NET 8/9/10. Has first-party Qdrant hosting integration. Use 13.x, NOT 9.2 — the project constraint "Aspire v9.2" is outdated; 13.x is the current stable line. |
| Microsoft.Extensions.AI | 10.4.x | Embedding abstraction layer | GA (no longer preview). Provides `IEmbeddingGenerator<string, Embedding<float>>` — the standard .NET abstraction for pluggable embedding providers. 3M+ downloads. This is THE way to do configurable embeddings in .NET. |
| xUnit v3 | 3.2.2 | Test framework | Latest stable with MTP (Microsoft Testing Platform) support. Use `xunit.v3` meta-package. Set `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>` in test projects. |

### Embedding Providers (Pluggable via Microsoft.Extensions.AI)

| Provider Package | Version | Purpose | When to Use |
|-----------------|---------|---------|-------------|
| Microsoft.Extensions.AI.OpenAI | 10.4.1 | OpenAI / Azure OpenAI embeddings | Cloud users wanting text-embedding-3-small/large. Also works with any OpenAI-compatible endpoint. |
| OllamaSharp | 5.4.16 | Local Ollama embeddings | Users running Ollama locally. Implements `IEmbeddingGenerator` natively. Recommended over deprecated `Microsoft.Extensions.AI.Ollama`. |
| SmartComponents.LocalEmbeddings | latest | Fully local ONNX embeddings | Users wanting zero-network, in-process embeddings. Small model, fast, no external dependencies. |

### Aspire Integration

| Package | Version | Purpose | Notes |
|---------|---------|---------|-------|
| Aspire.AppHost.Sdk | 13.1.3+ | AppHost SDK for orchestration | Referenced as SDK in AppHost `.csproj`. |
| Aspire.Hosting.Qdrant | 13.1.0+ | Qdrant container hosting | Provides `AddQdrant()` in AppHost. Runs `qdrant/qdrant` container. |
| Aspire.Qdrant.Client | 13.1.2+ | DI-registered QdrantClient | Registers `QdrantClient` in service DI from Aspire service discovery. |
| Aspire.Hosting.Testing | 13.2.0 | Integration test infrastructure | `DistributedApplicationTestingBuilder` for spinning up AppHost in tests. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| YamlDotNet | 16.3.0 | YAML frontmatter parsing | Parsing skill file frontmatter (name, description, tags). Use `IDeserializer` with `Parser` to extract frontmatter block, then deserialize to strongly-typed model. |
| System.CommandLine | 2.0.x | CLI argument parsing | For `--console`, `--setup`, `--names`, `--summaries` flags. Microsoft's official CLI parsing library. |
| System.Text.Json | (built-in) | JSON serialization | Built into .NET 10. Use for `--console` JSON output and config file reading/writing. No external package needed. |
| Microsoft.Extensions.Hosting | (built-in) | Host builder | Foundation for MCP server. `Host.CreateApplicationBuilder(args)` pattern. |
| Microsoft.Extensions.Options | (built-in) | Configuration binding | Bind `appsettings.json` / env vars to strongly-typed options (Qdrant endpoint, embedding provider config, API key). |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| Docker Desktop | Qdrant container via Aspire | Aspire's `AddQdrant()` runs the container automatically. |
| .NET 10 SDK | Build, test, pack | Install from dotnet.microsoft.com. Includes `dnx` tool runner. |
| Aspire workload | Aspire AppHost support | `dotnet workload install aspire` after SDK install. |

## Installation

```bash
# .NET 10 SDK (prerequisite)
# Download from https://dotnet.microsoft.com/download/dotnet/10.0

# Aspire workload
dotnet workload install aspire

# Core project packages
dotnet add package ModelContextProtocol --version 1.1.0
dotnet add package Qdrant.Client --version 1.17.0
dotnet add package Microsoft.Extensions.AI --version 10.4.0
dotnet add package YamlDotNet --version 16.3.0

# Embedding providers (user picks one or more)
dotnet add package Microsoft.Extensions.AI.OpenAI --version 10.4.1
dotnet add package OllamaSharp --version 5.4.16

# AppHost project
dotnet add package Aspire.Hosting.Qdrant --version 13.1.0

# Service project (Aspire client integration)
dotnet add package Aspire.Qdrant.Client --version 13.1.2

# Test project
dotnet add package xunit.v3 --version 3.2.2
dotnet add package Aspire.Hosting.Testing --version 13.2.0
```

## NuGet Tool Packaging

The project distributes as a .NET tool via NuGet, invokable with `dnx QdrantSkillsMCP`.

```xml
<!-- In the main project .csproj -->
<PropertyGroup>
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>QdrantSkillsMCP</ToolCommandName>
  <PackageOutputPath>./nupkg</PackageOutputPath>
  <PackageId>QdrantSkillsMCP</PackageId>
</PropertyGroup>
```

**dnx (new in .NET 10):** Downloads and runs a tool package without explicit install. Users run `dnx QdrantSkillsMCP` and it just works. Falls back to `dotnet tool install -g QdrantSkillsMCP` for pre-.NET 10 users.

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| ModelContextProtocol (official) | ModelContextProtocol.NET (community) | Never — the community package (0.3.x alpha) predates the official SDK and is not maintained by the MCP spec authors. |
| Qdrant.Client (official gRPC) | Aerx.QdrantClient.Http | Never for this project — HTTP client lacks gRPC performance and the Aspire integration registers `QdrantClient` specifically. |
| Microsoft.Extensions.AI | Semantic Kernel embeddings | If the project later needs SK's full agent/planner capabilities. For just embeddings, M.E.AI is lighter and more composable. |
| OllamaSharp | Microsoft.Extensions.AI.Ollama | Never — Microsoft deprecated their Ollama package in favor of OllamaSharp. |
| YamlDotNet | Markdig.Extensions.Yaml | If you need full Markdown AST parsing too. YamlDotNet alone is simpler when you only need frontmatter extraction. |
| xUnit v3 | NUnit 4 / MSTest 3 | Personal preference only. xUnit v3 has the best MTP integration and is the .NET community standard. |
| Aspire 13.x | Docker Compose | If Aspire is overkill. But Aspire provides service discovery, health checks, and test infrastructure — worth it for this project. |
| System.CommandLine | Spectre.Console.Cli | If you want richer CLI UX (tables, colors). System.CommandLine is sufficient for the flags described in requirements. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Aspire 9.2 packages | Outdated. Aspire jumped to v13 in Nov 2025. The 9.x line is end-of-support. | Aspire 13.x packages |
| Microsoft.Extensions.AI.Ollama | Officially deprecated by Microsoft. No future updates. | OllamaSharp 5.x |
| ModelContextProtocol.NET.Core / .Server | Community alpha packages (0.3.x). Not the official SDK. Will confuse NuGet resolution. | ModelContextProtocol 1.1.0 (official) |
| ModelContextProtocol.AspNetCore | For HTTP/SSE transport MCP servers. This project uses stdio transport. | ModelContextProtocol (main package) |
| Pinecone / Weaviate / Milvus clients | Wrong vector DB. Project requires Qdrant. | Qdrant.Client |
| xUnit v2 | Legacy. Does not support MTP runner. v3 is stable and GA. | xunit.v3 3.2.2 |
| Microsoft.SemanticKernel (for embeddings only) | Heavyweight dependency (pulls in planners, agents, memory). Overkill when only `IEmbeddingGenerator` is needed. | Microsoft.Extensions.AI |

## Stack Patterns by Variant

**If user wants fully local (no cloud API calls):**
- Use OllamaSharp + local Ollama with `all-minilm` or `nomic-embed-text` model
- Or SmartComponents.LocalEmbeddings for in-process ONNX
- Qdrant runs locally via Aspire container
- Zero network dependencies beyond NuGet restore

**If user wants cloud embeddings (OpenAI/Azure):**
- Use Microsoft.Extensions.AI.OpenAI with `text-embedding-3-small`
- Configure via `appsettings.json` or environment variables
- Same `IEmbeddingGenerator` interface — no code changes

**If user wants both (configurable at runtime):**
- Register embedding provider via DI based on config
- Factory pattern: read `EmbeddingProvider` from config, resolve appropriate `IEmbeddingGenerator` implementation
- This is the recommended default architecture

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| ModelContextProtocol 1.1.0 | .NET 8+ | Works on .NET 10. Uses Microsoft.Extensions.Hosting. |
| Qdrant.Client 1.17.0 | .NET 6+ / netstandard2.0 | Broad compatibility. Depends on Grpc.Net.Client >= 2.71.0. |
| Aspire 13.x packages | .NET 8, 9, 10 | Aspire 13 explicitly supports all three. |
| xunit.v3 3.2.2 | .NET 8+ | MTP v1 by default, v2 opt-in available. |
| Microsoft.Extensions.AI 10.4.x | .NET 9+ | Check minimum TFM — may require net9.0 or net10.0. |
| YamlDotNet 16.3.0 | .NET 6+ / netstandard2.0 | Broad compatibility. Supports AOT via source generator. |
| OllamaSharp 5.4.16 | .NET 8+ | Implements M.E.AI interfaces natively. |

## Key Architecture Decision: Aspire Version

The PROJECT.md states "Aspire v9.2" as a constraint. This is **outdated and must be updated**:

- Aspire 9.2 was released mid-2025
- In November 2025, Aspire jumped to **v13** (intentionally skipping 10-12 to decouple from .NET versioning)
- Aspire 9.x is now **out of support**
- Aspire 13.x is the current stable line, with 13.2.0 as latest
- All Qdrant integration packages (Hosting, Client) are versioned at 13.x
- The Aspire.Hosting.Testing package for xUnit integration tests is at 13.2.0

**Recommendation:** Update the project constraint from "Aspire v9.2" to "Aspire 13.x (latest stable)".

## Sources

- [NuGet: ModelContextProtocol 1.1.0](https://www.nuget.org/packages/ModelContextProtocol/) -- version and download count verified
- [GitHub: modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) -- official SDK repo
- [Microsoft .NET Blog: Build MCP server in C#](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/) -- WithTools/WithStdio pattern
- [NuGet: Qdrant.Client 1.17.0](https://www.nuget.org/packages/Qdrant.Client) -- version verified
- [Aspire Qdrant integration docs](https://learn.microsoft.com/en-us/dotnet/aspire/database/qdrant-component) -- hosting + client setup
- [NuGet: Aspire.Hosting.Qdrant 13.1.0](https://www.nuget.org/packages/Aspire.Hosting.Qdrant) -- version verified
- [NuGet: Aspire.Qdrant.Client 13.1.2](https://www.nuget.org/packages/Aspire.Qdrant.Client) -- version verified
- [NuGet: Aspire.Hosting.Testing 13.2.0](https://www.nuget.org/packages/aspire.hosting.testing) -- version verified
- [NuGet: Aspire.AppHost.Sdk 13.1.3](https://www.nuget.org/packages/Aspire.AppHost.Sdk) -- version verified
- [Microsoft .NET Blog: AI and Vector Data Extensions GA](https://devblogs.microsoft.com/dotnet/ai-vector-data-dotnet-extensions-ga/) -- M.E.AI GA announcement
- [NuGet: Microsoft.Extensions.AI 10.4.0](https://www.nuget.org/packages/Microsoft.Extensions.AI/) -- version verified
- [NuGet: Microsoft.Extensions.AI.OpenAI 10.4.1](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI/) -- version verified
- [NuGet: OllamaSharp 5.4.16](https://www.nuget.org/packages/OllamaSharp) -- version verified, implements IEmbeddingGenerator
- [NuGet: xunit.v3 3.2.2](https://www.nuget.org/packages/xunit.v3) -- version verified
- [xUnit v3 MTP docs](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform) -- MTP setup
- [NuGet: YamlDotNet 16.3.0](https://www.nuget.org/packages/YamlDotNet) -- version verified
- [Microsoft: Aspire 13 announcement](https://devblogs.microsoft.com/dotnet/dotnet-aspire-92-is-now-available-with-new-ways-to-deploy/) -- versioning change rationale
- [Visual Studio Magazine: Aspire 13 drops .NET branding](https://visualstudiomagazine.com/articles/2025/11/12/microsoft-releases-aspire-13.aspx) -- Aspire rebranding context
- [Andrew Lock: dnx tool runner in .NET 10](https://andrewlock.net/exploring-dotnet-10-preview-features-5-running-one-off-dotnet-tools-with-dnx/) -- dnx packaging details
- [.NET 10 download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) -- GA confirmed Nov 2025

---
*Stack research for: .NET MCP Server with Qdrant Vector Storage*
*Researched: 2026-03-25*
