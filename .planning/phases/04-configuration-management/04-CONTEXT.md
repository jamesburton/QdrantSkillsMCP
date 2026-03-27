# Phase 4: Configuration Management - Context

**Gathered:** 2026-03-27
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can configure Qdrant connection (local/remote), collection name, API keys, and embedding provider via a `--config` command, environment variables, and cross-platform env var helpers. Named profiles allow switching between environments. This phase adds configuration UX on top of existing QdrantSkillsOptions infrastructure.

</domain>

<decisions>
## Implementation Decisions

### Config command UX
- Both interactive wizard (--config with no args) and get/set CLI (--config show/set/get/reset/init/validate) — follows the --setup pattern
- Operations: show (display all config with sources), set, get, validate (test Qdrant connection + embedding provider), reset (key to default or all), init (generate starter config)
- Default write scope is user-level (~/.qdrant-skills/config.json); use --project flag for project-level (qdrant-skills.json)
- Secrets (API keys) masked by default in --config show output (sk-****7f3a); --reveal flag shows full values

### Config file location & format
- User-level config: ~/.qdrant-skills/config.json (same directory as FrequentSkills)
- Project-level config: qdrant-skills.json (unchanged from current)
- Precedence: Environment variables > Project config > User config > Defaults
- --config show displays source annotation per value: [default], [user], [project], or [env:QDRANT_SKILLS__*]

### Env var helper
- --config env generates a copy-pasteable shell snippet with all configurable env vars as a commented template
- Current values filled in where set; user uncomments what they need
- Auto-detect shell (bash/zsh via $SHELL, PowerShell via $PSVersionTable, fallback to bash)
- Output matching format: export for bash/zsh, $env: for PowerShell, set for cmd
- Covers all var groups: Qdrant connection, embedding provider, Azure OpenAI

### Named profiles
- Profiles stored as named sections in ~/.qdrant-skills/config.json
- Active profile tracked in same file
- --config use <name> switches active profile
- Built-in 'local' preset ships pre-configured (localhost:6334, no TLS, no API key, 'skills' collection)
- --config init creates the local preset by default
- Non-localhost hosts trigger TLS auto-detection/warning during validate

### Claude's Discretion
- Config JSON schema details (flat vs nested keys)
- Profile section naming convention in config.json
- Exact TLS auto-detection heuristics
- Interactive wizard question flow and ordering
- Validate command output format (pass/fail per check)

</decisions>

<specifics>
## Specific Ideas

- Git-style config experience: --config show like `git config --list --show-origin`
- Profile switching like AWS CLI profiles or kubectl contexts: `--config use cloud`
- Env var template like a .env.example: all vars commented out with descriptions, uncomment what you need
- --config validate as a "does everything work" health check after changing config

</specifics>

<code_context>
## Existing Code Insights

### Reusable Assets
- QdrantSkillsOptions (Configuration/QdrantSkillsOptions.cs): Already has all config properties — host, port, API key, collection, embedding provider, Azure settings. Phase 4 adds UX to manage these.
- ServiceRegistration.cs: Config binding via IOptions pattern already wired. RegisterEmbeddingProvider reads from IConfiguration at registration time.
- Spectre.Console: Already used in SetupWizard for interactive prompts (MultiSelectionPrompt, SelectionPrompt). Reusable for --config interactive mode.
- FrequentSkillsService: Already uses ~/.qdrant-skills/ directory. Config file goes alongside.

### Established Patterns
- Program.cs mode branching: --console, --setup, default MCP. --config adds a fourth branch.
- Host.CreateApplicationBuilder pattern: Config sources added via builder.Configuration.AddJsonFile().
- IOptions<QdrantSkillsOptions> binding from "QdrantSkills" config section.
- Environment variable prefix: QDRANT_SKILLS__ already works via .NET config provider (double underscore = section separator).

### Integration Points
- Program.cs: New --config branch (before --console and --setup checks, or as a peer)
- builder.Configuration: Add user-level config source (~/.qdrant-skills/config.json) before project-level
- QdrantSkillsOptions: May need profile-aware properties or a wrapper
- QdrantClient constructor: Currently takes host/port/apiKey — may need HTTPS toggle for TLS

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 04-configuration-management*
*Context gathered: 2026-03-27*
