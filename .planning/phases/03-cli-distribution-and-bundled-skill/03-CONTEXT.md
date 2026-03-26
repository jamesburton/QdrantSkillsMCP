# Phase 3: CLI, Distribution, and Bundled Skill - Context

**Gathered:** 2026-03-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Console CLI mode (`--console`) with single-shot subcommands and interactive REPL. Multi-agent setup wizard (`--setup`) that detects installed agents and writes MCP config entries. Bundled SKILL.md that teaches agents how to use QdrantSkillsMCP. NuGet tool packaging for distribution via `dnx qdrant-skills-mcp`. Auth, skills-guru integration, and new MCP tools are out of scope.

</domain>

<decisions>
## Implementation Decisions

### Console CLI output format
- Human-readable output by default (tables, formatted text)
- `--json` flag switches to JSON output for scripting and agent consumption
- Both modes write to stdout (CLI mode does NOT use MCP transport — no stdout pollution risk)

### Console subcommands
- `search <query>` — semantic search, mirrors search-skills MCP tool
- `list` — list all skills, mirrors list-skills MCP tool
- `load <name>` — load specific skill by name, mirrors load-skill MCP tool
- `add` / `update` / `delete` / `archive` — full CRUD, mirrors MCP CRUD tools
- `status` / `info` — show connection info, collection stats, configured provider, diagnostics

### REPL mode
- `--console` with no subcommand enters interactive REPL
- Rich REPL with command history and tab completion for skill names
- Exit with `quit`, `exit`, or Ctrl+C

### Mode branching in Program.cs
- Args-based branching: Program.cs checks for `--console` and `--setup` early
- If `--console` or `--setup` present → route to CLI handler (no MCP server startup)
- If neither present → current MCP server behavior (unchanged)
- Single entry point, single project (Infrastructure)

### Setup wizard — agent detection
- Config file probing: check known filesystem paths for each agent's config file
- Supported agents: Claude, Copilot, Codex, opencode, docker-agent, kilocode, factory-droid (and others with known config paths)
- If config file or parent directory exists → agent is detected as installed

### Setup wizard — config writing
- Always backup existing config file to `.bak` before modifying
- Merge QdrantSkillsMCP MCP server entry into agent's existing config
- For agents with unknown config formats: print a JSON/YAML snippet to console with copy-paste instructions
- Non-interactive mode: `--setup --agent claude --level user` (specify agent and scope via flags)

### Setup wizard — interactive flow
- Auto-detect installed agents and show the list
- User confirms/deselects agents from the list
- Configure all selected agents in one pass
- Supports both project-level and user-level configuration per agent

### Bundled SKILL.md — content
- Tool usage guide: teaches agents WHEN and HOW to call each MCP tool
- Covers: search-before-load pattern, output modes for large skill sets, session tracking awareness
- Operational playbook, not a full parameter reference

### Bundled SKILL.md — delivery
- **Primary:** Embedded resource in the NuGet package. `--setup` writes SKILL.md to the agent's skill directory (e.g., `~/.claude/skills/qdrant-skills-mcp/`)
- **Alternative:** `get-skill-guide` MCP tool returns SKILL.md content on demand (agents can request it without file placement)
- **Bootstrap:** A tiny `enable-skill-search` skill that triggers MCP check/install for agents that don't have the full skill yet

### Frequent skills — dual-file system
- Two files: `FrequentSkills.md` (shared, committable) and `FrequentSkills.local.md` (personal, gitignored)
- **Two-tier location:**
  - User-level: `~/.qdrant-skills/FrequentSkills.md` and `~/.qdrant-skills/FrequentSkills.local.md` (global defaults)
  - Project-level: project root `FrequentSkills.md` and `FrequentSkills.local.md` (project-specific overrides)
- Merge order: user-level → project-level → project-local (most specific wins)
- SKILL.md instructs agents to read these files for pre-loaded skill awareness
- Skill tracks user preference for shared vs personal and checks `.gitignore` exclusion for `*.local.*` files if they appear in git changes

### NuGet tool packaging
- Package ID: `QdrantSkillsMCP`
- Tool command name: `qdrant-skills-mcp`
- Invocation: `dnx qdrant-skills-mcp`
- Add `PackAsTool` and `ToolCommandName` to existing Infrastructure.csproj (no new project)
- Portable / framework-dependent (requires .NET 10 runtime)

### Claude's Discretion
- REPL library choice (Spectre.Console, ReadLine, or custom)
- Tab completion implementation for skill names in REPL
- Exact config file paths for each supported agent
- JSON merge strategy for agent config files
- Bootstrap skill (`enable-skill-search`) exact content and placement
- Status/info command output format and fields

</decisions>

<specifics>
## Specific Ideas

- **Dual-file frequent skills with gitignore awareness**: FrequentSkills.md is shared (team curated), FrequentSkills.local.md is personal. The skill should proactively check `.gitignore` exclusion for `*.local.*` patterns when it detects local files in git changes.
- **Multi-delivery SKILL.md**: Default to embedded resource placed by `--setup`, but also expose via MCP tool for agents that prefer on-demand access. The tiny bootstrap skill (`enable-skill-search`) is the minimal entry point for agents that don't have the full skill yet.
- **Future sync consideration**: The frequent skills system may later support sync to/from a shared repo or Qdrant instance. Not in scope now, but the dual-file design should not preclude it.
- **Rich REPL**: Tab completion for skill names makes the REPL genuinely useful for manual exploration. Command history persists across sessions.

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SkillSearchTools`, `SkillCrudTools`, `SessionTools`: All business logic for CLI subcommands already exists in these MCP tool classes — CLI layer calls the same service interfaces
- `ServiceRegistration.AddQdrantSkillsInfrastructure()`: DI wiring for all services — CLI mode reuses the same service registration
- `QdrantSkillsOptions`: Configuration model already supports all providers and settings — CLI just adds new flag bindings

### Established Patterns
- `Host.CreateApplicationBuilder(args)`: Already accepts CLI args — branching logic hooks in before `Build().RunAsync()`
- All logging to stderr via `LogToStandardErrorThreshold = LogLevel.Trace`: CLI mode can relax this (stdout is safe in CLI mode)
- `[McpServerToolType]` classes with constructor DI: CLI handler can resolve the same service interfaces from the DI container

### Integration Points
- `Program.cs`: Mode branching point — check args before MCP server setup
- `Infrastructure.csproj`: Add `PackAsTool`, `ToolCommandName`, `PackageId` properties
- SKILL.md: Embedded resource in Infrastructure project
- `--setup`: Reads from embedded agent config templates, writes to filesystem

</code_context>

<deferred>
## Deferred Ideas

- **Frequent skills sync to Qdrant or shared repo** — potential future enhancement for team-level skill sharing beyond git-committed FrequentSkills.md
- **skills-guru integration** — already tracked as v2 requirement (ECO-01, ECO-02)
- **ONNX companion NuGet package (`QdrantSkillsMCP.Models.DefaultEmbedding`)** — deferred because the RESEARCH.md flags the contentFiles discovery mechanism as an open question. The auto-download from HuggingFace (already implemented in Phase 2) provides a working fallback. Will revisit when the NuGet contentFiles approach is validated.

</deferred>

---

*Phase: 03-cli-distribution-and-bundled-skill*
*Context gathered: 2026-03-26*
