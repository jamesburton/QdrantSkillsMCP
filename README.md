# QdrantSkillsMCP

A .NET 10 MCP server for vector-based skill storage and retrieval using Qdrant. Enables AI agents (Claude Code, Copilot, Codex, etc.) to semantically search, load, and manage skills via MCP tools.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (required)
- A Qdrant instance — local via Docker/Aspire, or a hosted service like [Qdrant Cloud](https://cloud.qdrant.io/)

## Get Started

**No install needed** — `dnx` runs the tool directly from NuGet, always using the latest version:

```bash
# Initialize config (creates ~/.qdrant-skills/config.json with local defaults)
dnx QdrantSkillsMCP -- --config init

# Auto-configure your AI agent (Claude, Copilot, Codex, etc.)
dnx QdrantSkillsMCP -- --setup

# Verify your Qdrant connection
dnx QdrantSkillsMCP -- --config validate
```

> **What is `dnx`?** It's .NET 10's equivalent of `npx` — runs NuGet tools without installing them. Always gets the latest version automatically.

### Alternative: Global Install

If you prefer a permanent installation (no `dnx` prefix needed):

```bash
dotnet tool install -g QdrantSkillsMCP
qdrant-skills-mcp --config init
qdrant-skills-mcp --setup
```

Update later with: `dotnet tool update -g QdrantSkillsMCP`

## Configuration

```bash
# Show all config with source annotations ([default], [user], [project], [env])
dnx QdrantSkillsMCP -- --config show

# Connect to a remote Qdrant instance
dnx QdrantSkillsMCP -- --config set QdrantHost=my-qdrant.example.com
dnx QdrantSkillsMCP -- --config set QdrantGrpcPort=6334
dnx QdrantSkillsMCP -- --config set UseTls=true
dnx QdrantSkillsMCP -- --config set QdrantApiKey=your-api-key

# Named profiles for switching between environments
dnx QdrantSkillsMCP -- --config use cloud

# Validate connection works
dnx QdrantSkillsMCP -- --config validate

# Generate env var template for your shell (auto-detects bash/PowerShell/cmd)
dnx QdrantSkillsMCP -- --config env

# Interactive config wizard
dnx QdrantSkillsMCP -- --config
```

Config files:
- **User-level:** `~/.qdrant-skills/config.json` (API keys, personal settings)
- **Project-level:** `./qdrant-skills.json` (shared team settings)
- **Precedence:** Environment variables > Project > User > Defaults

## CLI Usage

```bash
# Search skills by meaning
dnx QdrantSkillsMCP -- --console search "authentication patterns"

# List all skills
dnx QdrantSkillsMCP -- --console list

# JSON output for scripting
dnx QdrantSkillsMCP -- --console --json search "error handling"

# Interactive REPL with tab completion and history
dnx QdrantSkillsMCP -- --console

# Show help
dnx QdrantSkillsMCP -- --console help
```

## MCP Server Mode

By default (no flags), QdrantSkillsMCP runs as an MCP server over stdio. This is how AI agents connect to it. The `--setup` wizard configures this automatically for your agent.

### Available MCP Tools

| Tool | Description |
|------|-------------|
| `search-skills` | Semantic vector search with configurable temperature and max results |
| `load-skill` | Fetch specific skill(s) by name |
| `add-skill` | Persist a skill with YAML frontmatter to Qdrant |
| `update-skill` | Update existing skill content and re-embed |
| `delete-skill` | Permanently remove a skill |
| `archive-skill` | Soft-hide a skill without deletion |
| `list-skills` | List all skills (supports `--names` and `--summaries` modes) |
| `reset-session` | Clear session tracking for loaded skills |
| `get-skill-guide` | Returns the bundled guide teaching agents how to use QdrantSkillsMCP |

## ONNX Model Packages

For local embedding without API keys, install a companion model package:

| Package | Model | Size | Quality | Dims |
|---------|-------|------|---------|------|
| `QdrantSkillsMCP.Models.MiniLM` | all-MiniLM-L6-v2 | ~23 MB | Fastest | 384 |
| `QdrantSkillsMCP.Models.BgeSmall` | BGE-small-en-v1.5 | ~34 MB | Best value | 384 |
| `QdrantSkillsMCP.Models.BgeBase` | BGE-base-en-v1.5 | ~105 MB | Highest quality | 768 |

Install alongside the main tool:

```bash
dotnet tool install -g QdrantSkillsMCP
dotnet tool install -g QdrantSkillsMCP.Models.BgeSmall
```

To select a non-default model:

```bash
dnx QdrantSkillsMCP -- --config set OnnxModelName=bge-small-en-v1.5
```

Without a companion package, the tool auto-downloads all-MiniLM-L6-v2 on first use.

## Embedding Providers

Configure via `dnx QdrantSkillsMCP -- --config set EmbeddingProvider=<provider>`:

| Provider | Model | Notes |
|----------|-------|-------|
| **LocalONNX** (default) | all-MiniLM-L6-v2 | Runs locally, no API key needed, 384 dimensions |
| **OpenAI** | text-embedding-3-small/large | Requires `OpenAiApiKey` or `OPENAI_API_KEY` env var |
| **Ollama** | Any Ollama embedding model | Set `EmbeddingUrl` (default: http://localhost:11434) |
| **AzureOpenAI** | Azure-hosted embeddings | Requires endpoint, key, and deployment name |

## Development

Requires .NET 10 SDK and Docker (for Qdrant via Aspire).

```bash
# Run with Aspire (starts Qdrant automatically)
dotnet run --project src/QdrantSkillsMCP.AppHost

# Run unit tests
dotnet test tests/QdrantSkillsMCP.UnitTests

# Run all tests (requires Qdrant running)
dotnet test
```

## License

MIT
