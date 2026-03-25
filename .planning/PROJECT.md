# QdrantSkillsMCP

## What This Is

A .NET 10 C# MCP (Model Context Protocol) server that provides vector-based skill storage and retrieval using Qdrant. It allows AI agents (Claude Code, Copilot, Codex, etc.) to search, load, add, update, archive, and delete Claude Code skills (markdown with YAML frontmatter) via MCP tools. Skills are embedded and stored in Qdrant for semantic vector search, enabling agents to find relevant skills based on context and prompt content.

## Core Value

Agents can semantically search and retrieve the right skills at the right time — turning a flat collection of skill files into an intelligent, context-aware skill library accessible to any MCP-compatible agent.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] MCP server runs via `--stdio` for standard MCP transport
- [ ] Connects to configurable Qdrant instance (default: localhost:6334, `skills` collection)
- [ ] `search-skills` tool: vector-based semantic search with configurable temperature, max-results, context summary + prompt input
- [ ] `load-skill` tool: fetch specific skill(s) by name, supports reloading updated skills
- [ ] `add-skill` / `update-skill` tools: persist skills (markdown with YAML frontmatter) to Qdrant with vector embeddings
- [ ] `archive-skill` tool: soft-hide obsolete skills without deletion
- [ ] `delete-skill` tool: permanently remove skills from the collection
- [ ] Configurable embedding provider (local model, OpenAI API, or other — user chooses)
- [ ] `--console` parameter: single-shot CLI subcommands with JSON output, or REPL if no subcommand given
- [ ] `--setup` command: auto-configures MCP server entry in agent config files (claude, copilot, codex, opencode, docker-agent, kilocode, factory-droid, others); auto-writes config where possible, falls back to snippets; supports project-level and user-level; interactive if no args provided
- [ ] `--names` option: return skill names only (for large skill sets)
- [ ] `--summaries` option: return name + short summary (preview before full load)
- [ ] Session tracking: tracks which skills have been returned per session; includes `ALREADY LOADED SKILLS: {list}` in search results
- [ ] Session identification: defaults to MCP connection lifecycle, supports explicit session ID override
- [ ] Authentication: API key (bearer token) for simple cases, OAuth/OIDC for enterprise — enabled in server mode
- [ ] Packaged as NuGet tool, invoked via `dnx QdrantSkillsMCP`
- [ ] Bundled SKILL.md: a skill file that teaches agents how to use QdrantSkillsMCP effectively + curated short-list of frequently used skills to reduce search calls
- [ ] skills-guru integration: full integration as a first-class backend — push/sync skills TO QdrantSkillsMCP and query/search FROM it
- [ ] Local development via Aspire v13.2 AppHost running Qdrant via Aspire integration
- [ ] Full XUnit v3 (MTP) test coverage using Aspire testing framework

### Out of Scope

- GUI / web dashboard — CLI and MCP tools only
- Skill authoring/editing UI — skills are authored as markdown files externally
- Multi-tenant SaaS hosting — this is a local/self-hosted tool
- Real-time collaborative editing of skills
- Non-.NET client SDKs — agents interact via MCP protocol

## Context

- **MCP Server Foundation**: Built on the official `ModelContextProtocol` C# SDK NuGet package, using `AddMcpServer().WithStdioServerTransport().WithTools()` pattern from Microsoft Agent Framework docs
- **Skill Format**: Claude Code skills — markdown files with YAML frontmatter (name, description, etc.) + markdown body. Full spec at https://code.claude.com/docs/en/skills
- **skills-guru**: Existing Claude Code skill at `~/.claude/skills/skills-guru/` (GitHub: jamesburton/skills-guru) that manages skill installation, scoping, sync, and catalog. QdrantSkillsMCP becomes a storage/search backend for it
- **Qdrant**: Open-source vector database. Aspire has a community integration for local development. Default collection name `skills`, default endpoint `localhost:6334`
- **Embedding Models**: Must be pluggable — some users want local (ONNX/Ollama), others want OpenAI API, others may want Azure OpenAI or other providers
- **Target Agents**: claude, copilot, codex, opencode, docker-agent, kilocode, factory-droid — each has its own MCP config file format/location

## Constraints

- **Runtime**: .NET 10 — required for latest Aspire and Agent Framework compatibility
- **Test Framework**: XUnit v3 with MTP (Microsoft Testing Platform) runner — no older xunit versions
- **Aspire Version**: v13.2 for AppHost and testing infrastructure (Microsoft skipped versions 10-12)
- **Package Distribution**: NuGet tool package, invoked via `dnx QdrantSkillsMCP`
- **MCP Transport**: stdio as primary transport (standard for local MCP servers)
- **Skill Schema**: Must preserve full Claude Code skill format (frontmatter + markdown body) — no lossy transformations

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Use ModelContextProtocol C# SDK for MCP server | Official SDK, maintained by MCP spec authors, integrates with Agent Framework | — Pending |
| Qdrant as vector store | Open-source, has Aspire integration, proven at scale, good .NET client | — Pending |
| Configurable embedding providers | Users have different constraints (offline, cost, latency) — can't mandate one provider | — Pending |
| dnx tool packaging | Standard .NET tool distribution, easy installation across machines | — Pending |
| API key + OAuth dual auth | Simple for dev/personal use, enterprise-ready with OAuth | — Pending |
| Session tracking per MCP connection | Natural session boundary for MCP, with override for advanced use cases | — Pending |

---
*Last updated: 2026-03-25 after initialization*
