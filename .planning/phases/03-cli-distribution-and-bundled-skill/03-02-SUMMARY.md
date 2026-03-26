---
phase: 03-cli-distribution-and-bundled-skill
plan: 02
subsystem: cli
tags: [setup-wizard, agent-detection, config-writing, json-merge, toml, spectre-console, skill-placement]

# Dependency graph
requires:
  - phase: 01-core-mcp-server
    provides: Infrastructure project structure and service registration
provides:
  - IAgentConfigWriter interface with SkillDirectoryPath for per-agent config writing
  - AgentDetector for filesystem-based agent detection
  - 8 config writers (Claude, ClaudeDesktop, Copilot, CopilotCli, Codex, opencode, KiloCode, factory-droid)
  - SnippetFallbackWriter for unknown agents
  - SetupWizard with interactive and non-interactive modes and SKILL.md placement
affects: [03-03-bundled-skill]

# Tech tracking
tech-stack:
  added: [Tomlyn 0.17.0, Spectre.Console 0.50.0]
  patterns: [JsonConfigWriterBase for JSON merge, backup-before-write, embedded resource SKILL.md delivery]

key-files:
  created:
    - src/QdrantSkillsMCP.Infrastructure/Setup/IAgentConfigWriter.cs
    - src/QdrantSkillsMCP.Infrastructure/Setup/McpServerEntry.cs
    - src/QdrantSkillsMCP.Infrastructure/Setup/AgentDetector.cs
    - src/QdrantSkillsMCP.Infrastructure/Setup/SetupWizard.cs
    - src/QdrantSkillsMCP.Infrastructure/Setup/Writers/JsonConfigWriterBase.cs
    - src/QdrantSkillsMCP.Infrastructure/Setup/Writers/ClaudeConfigWriter.cs
    - src/QdrantSkillsMCP.Infrastructure/Setup/Writers/ClaudeDesktopConfigWriter.cs
    - src/QdrantSkillsMCP.Infrastructure/Setup/Writers/CopilotConfigWriter.cs
    - src/QdrantSkillsMCP.Infrastructure/Setup/Writers/CopilotCliConfigWriter.cs
    - src/QdrantSkillsMCP.Infrastructure/Setup/Writers/CodexConfigWriter.cs
    - src/QdrantSkillsMCP.Infrastructure/Setup/Writers/OpenCodeConfigWriter.cs
    - src/QdrantSkillsMCP.Infrastructure/Setup/Writers/KiloCodeConfigWriter.cs
    - src/QdrantSkillsMCP.Infrastructure/Setup/Writers/FactoryDroidConfigWriter.cs
    - src/QdrantSkillsMCP.Infrastructure/Setup/Writers/SnippetFallbackWriter.cs
    - tests/QdrantSkillsMCP.UnitTests/Setup/AgentDetectorTests.cs
    - tests/QdrantSkillsMCP.UnitTests/Setup/ConfigWriterTests.cs
    - tests/QdrantSkillsMCP.UnitTests/Setup/SetupWizardTests.cs
  modified:
    - src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj

key-decisions:
  - "JsonConfigWriterBase abstracts backup/merge/validate pattern for all JSON agents"
  - "Copilot uses 'servers' root key (not 'mcpServers') per VS Code MCP spec"
  - "opencode uses command-as-array format with 'mcp' root key"
  - "Only Claude Code has SkillDirectoryPath; other agents rely on get-skill-guide MCP tool"
  - "Codex uses Tomlyn for TOML read-modify-write (not hand-rolled)"

patterns-established:
  - "JsonConfigWriterBase: backup, JsonNode merge, validate pattern for all JSON config writers"
  - "IAgentConfigWriter: contract with SkillDirectoryPath for dual-delivery (file + MCP tool)"
  - "SetupWizard.ParseArgs: extracted static method for testable arg parsing"

requirements-completed: [CLI-03, CLI-04, CLI-05, CLI-06, CLI-07]

# Metrics
duration: 11min
completed: 2026-03-26
---

# Phase 3 Plan 2: Setup Wizard Summary

**Multi-agent setup wizard with 8 config writers, TOML/JSON merge, backup, and SKILL.md placement for Claude Code skill directory**

## Performance

- **Duration:** 11 min
- **Started:** 2026-03-26T00:49:33Z
- **Completed:** 2026-03-26T01:01:00Z
- **Tasks:** 3
- **Files modified:** 20

## Accomplishments
- IAgentConfigWriter interface with SkillDirectoryPath supporting dual-delivery (file placement + MCP tool)
- 8 auto-write config writers covering Claude, Copilot, Codex (TOML), opencode, KiloCode, factory-droid
- SetupWizard with non-interactive (`--agent claude --level user`) and interactive (Spectre.Console) modes
- SKILL.md embedded resource placement to agent skill directories during setup
- 51 unit tests covering detector, all writers, and wizard flows

## Task Commits

Each task was committed atomically:

1. **Task 1: Interface, detector, and JSON config writers** - `f03c118` (feat)
2. **Task 2: Remaining writers (Codex/TOML, opencode, kilocode, factory-droid, snippet)** - `8fce951` (feat)
3. **Task 3: SetupWizard with SKILL.md placement** - `6249424` (feat)

## Files Created/Modified
- `src/.../Setup/IAgentConfigWriter.cs` - Contract for per-agent config writing with skill directory support
- `src/.../Setup/McpServerEntry.cs` - MCP server entry, AgentScope, DetectedAgent records
- `src/.../Setup/AgentDetector.cs` - Filesystem probing via registered writers
- `src/.../Setup/SetupWizard.cs` - Interactive and non-interactive setup orchestration
- `src/.../Setup/Writers/JsonConfigWriterBase.cs` - Shared backup/merge/validate for JSON writers
- `src/.../Setup/Writers/ClaudeConfigWriter.cs` - mcpServers at ~/.claude.json, skill dir support
- `src/.../Setup/Writers/ClaudeDesktopConfigWriter.cs` - mcpServers at platform-specific Desktop path
- `src/.../Setup/Writers/CopilotConfigWriter.cs` - "servers" root key for VS Code format
- `src/.../Setup/Writers/CopilotCliConfigWriter.cs` - mcpServers with type:local
- `src/.../Setup/Writers/CodexConfigWriter.cs` - TOML format via Tomlyn
- `src/.../Setup/Writers/OpenCodeConfigWriter.cs` - "mcp" root key, command as array
- `src/.../Setup/Writers/KiloCodeConfigWriter.cs` - mcpServers at ~/.kilocode paths
- `src/.../Setup/Writers/FactoryDroidConfigWriter.cs` - mcpServers with type:stdio
- `src/.../Setup/Writers/SnippetFallbackWriter.cs` - Copy-paste snippet generation
- `tests/.../Setup/AgentDetectorTests.cs` - 5 detector tests
- `tests/.../Setup/ConfigWriterTests.cs` - 29 writer tests
- `tests/.../Setup/SetupWizardTests.cs` - 17 wizard tests

## Decisions Made
- Used JsonConfigWriterBase to abstract backup/merge/validate pattern -- avoids code duplication across 6 JSON writers
- Copilot uses "servers" root key per VS Code MCP specification (different from other agents)
- opencode uses command-as-array format `["dnx", "qdrant-skills-mcp"]` per its config spec
- Only Claude Code has SkillDirectoryPath; all other agents return null (rely on get-skill-guide MCP tool)
- Codex writer uses Tomlyn library for proper TOML read-modify-write (not string manipulation)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed CrudCommands build error from Plan 03-01**
- **Found during:** Task 1 (build step)
- **Issue:** CrudCommands.cs called `parser.Parse(content)` which returns a tuple, but passed it where `Skill` was expected
- **Fix:** Linter auto-corrected to use `parser.ToSkill(content)` which returns `Skill` directly
- **Files modified:** src/QdrantSkillsMCP.Infrastructure/Cli/Commands/CrudCommands.cs
- **Verification:** Build succeeds
- **Committed in:** f03c118 (Task 1 commit)

**2. [Rule 3 - Blocking] Fixed ReplLoop ref-in-lambda build error from Plan 03-01**
- **Found during:** Task 3 (test build step)
- **Issue:** `ref string tabPrefix` parameter used directly inside LINQ lambda, which is not allowed in C#
- **Fix:** Captured ref parameter into local variable before use in lambda
- **Files modified:** src/QdrantSkillsMCP.Infrastructure/Cli/ReplLoop.cs
- **Verification:** Build succeeds, all tests pass
- **Committed in:** 6249424 (Task 3 commit)

---

**Total deviations:** 2 auto-fixed (2 blocking issues from prior plan)
**Impact on plan:** Both fixes necessary to unblock compilation. No scope creep.

## Issues Encountered
None beyond the pre-existing build errors documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Setup wizard complete and ready for integration with Program.cs --setup routing
- SKILL.md embedded resource not yet created (Plan 03-03 bundles it)
- WriteSkillFileAsync gracefully handles missing embedded resource

---
*Phase: 03-cli-distribution-and-bundled-skill*
*Completed: 2026-03-26*
