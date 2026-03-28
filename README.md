# QdrantSkillsMCP

A .NET 10 MCP (Model Context Protocol) server for vector-based skill storage and retrieval using Qdrant. Enables AI agents (Claude Code, Copilot, Codex, etc.) to semantically search, load, and manage skills via MCP tools.

## Install

```bash
# .NET 10+ (ephemeral, no install)
dnx QdrantSkillsMCP --console help

# Or install globally
dotnet tool install -g QdrantSkillsMCP
qdrant-skills-mcp --console help
```

## Quick Start

```bash
# Initialize config with local Qdrant preset
qdrant-skills-mcp --config init

# Auto-configure your AI agent (Claude, Copilot, Codex, etc.)
qdrant-skills-mcp --setup

# Search skills via CLI
qdrant-skills-mcp --console search "authentication"

# Interactive REPL
qdrant-skills-mcp --console
```

## Configuration

```bash
# Show all config with sources
qdrant-skills-mcp --config show

# Set remote Qdrant host
qdrant-skills-mcp --config set QdrantHost=my-qdrant.example.com
qdrant-skills-mcp --config set UseTls=true
qdrant-skills-mcp --config set QdrantApiKey=your-api-key

# Switch between profiles
qdrant-skills-mcp --config use cloud

# Validate connection
qdrant-skills-mcp --config validate

# Generate env var template for your shell
qdrant-skills-mcp --config env
```

## MCP Tools

When running as an MCP server (default mode), exposes these tools to agents:

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
| `get-skill-guide` | Returns the bundled SKILL.md teaching agents how to use QdrantSkillsMCP |

## Embedding Providers

Configurable via `--config set EmbeddingProvider=<provider>`:

- **LocalONNX** (default) — all-MiniLM-L6-v2, runs locally, no API key needed
- **OpenAI** — text-embedding-3-small/large
- **Ollama** — any Ollama embedding model
- **AzureOpenAI** — Azure-hosted OpenAI embeddings

## Development

Requires .NET 10 SDK and Docker (for Qdrant via Aspire).

```bash
# Run with Aspire (starts Qdrant automatically)
dotnet run --project src/QdrantSkillsMCP.AppHost

# Run tests
dotnet test
```

## License

MIT
