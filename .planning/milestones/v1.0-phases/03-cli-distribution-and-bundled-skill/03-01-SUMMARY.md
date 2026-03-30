---
phase: 03-cli-distribution-and-bundled-skill
plan: 01
subsystem: cli
tags: [spectre-console, repl, cli, console-host, mode-branching]

# Dependency graph
requires:
  - phase: 01-core-mcp-server
    provides: ISkillRepository, IEmbeddingService, ServiceRegistration
  - phase: 02-search-intelligence-and-embedding-providers
    provides: Multiple embedding providers, ISessionTracker
provides:
  - "--console CLI mode with single-shot subcommands"
  - "Interactive REPL with tab completion and command history"
  - "Mode branching in Program.cs (--console, --setup, MCP server)"
  - "ConsoleOutputFormatter for human-readable tables and JSON"
  - "NuGet tool packaging properties (PackAsTool, ToolCommandName)"
affects: [03-02, 03-03]

# Tech tracking
tech-stack:
  added: [Spectre.Console 0.54.0]
  patterns: [IAnsiConsole per-call creation for testable Console.Out, Collection attribute for test isolation]

key-files:
  created:
    - src/QdrantSkillsMCP.Infrastructure/Cli/ConsoleHost.cs
    - src/QdrantSkillsMCP.Infrastructure/Cli/ConsoleOutputFormatter.cs
    - src/QdrantSkillsMCP.Infrastructure/Cli/ReplLoop.cs
    - src/QdrantSkillsMCP.Infrastructure/Cli/Commands/SearchCommand.cs
    - src/QdrantSkillsMCP.Infrastructure/Cli/Commands/ListCommand.cs
    - src/QdrantSkillsMCP.Infrastructure/Cli/Commands/LoadCommand.cs
    - src/QdrantSkillsMCP.Infrastructure/Cli/Commands/CrudCommands.cs
    - src/QdrantSkillsMCP.Infrastructure/Cli/Commands/StatusCommand.cs
    - tests/QdrantSkillsMCP.UnitTests/Cli/ConsoleHostTests.cs
    - tests/QdrantSkillsMCP.UnitTests/Cli/ReplLoopTests.cs
  modified:
    - src/QdrantSkillsMCP.Infrastructure/Program.cs

key-decisions:
  - "AnsiConsole.Create() per-call with AnsiConsoleOutput(Console.Out) for testable Spectre.Console output"
  - "Collection attribute on CLI test classes prevents Console.Out race conditions in parallel test runs"
  - "ReplLoop.ProcessCommandAsync extracted as public method for unit testing without Console I/O"

patterns-established:
  - "CLI command pattern: static class with RunAsync taking IServiceProvider, args, formatter, CancellationToken"
  - "ConsoleOutputFormatter dual-mode: human-readable Spectre.Console tables vs JSON serialization"

requirements-completed: [CLI-01, CLI-02]

# Metrics
duration: 14min
completed: 2026-03-26
---

# Phase 3 Plan 1: Console CLI Mode Summary

**Console CLI with --console mode branching, 8 subcommands (search/list/load/add/update/delete/archive/status), interactive REPL with tab completion, and Spectre.Console table output**

## Performance

- **Duration:** 14 min
- **Started:** 2026-03-26T00:49:42Z
- **Completed:** 2026-03-26T01:03:00Z
- **Tasks:** 2
- **Files modified:** 11

## Accomplishments
- Program.cs mode branching: --console routes to CLI, --setup placeholder, default MCP server mode unchanged
- ConsoleHost dispatches 8 subcommands plus interactive REPL fallback
- ConsoleOutputFormatter supports human-readable Spectre.Console tables and --json mode
- Interactive REPL with tab completion for skill names and command names, command history, and standard line editing
- 16 unit tests covering all subcommand dispatch, output formatting, REPL command processing

## Task Commits

Each task was committed atomically:

1. **Task 1: Mode branching, ConsoleHost, commands, and output formatter** - `ee7ea2b` (feat)
2. **Task 2: Interactive REPL with tab completion and command history** - `f57b9c9` (feat)

_TDD flow: tests written first (RED), then implementation (GREEN) for both tasks._

## Files Created/Modified
- `src/QdrantSkillsMCP.Infrastructure/Program.cs` - Mode branching for --console, --setup, MCP server
- `src/QdrantSkillsMCP.Infrastructure/Cli/ConsoleHost.cs` - CLI entry point with subcommand dispatch
- `src/QdrantSkillsMCP.Infrastructure/Cli/ConsoleOutputFormatter.cs` - Dual-mode output (tables/JSON)
- `src/QdrantSkillsMCP.Infrastructure/Cli/ReplLoop.cs` - Interactive REPL with tab completion and history
- `src/QdrantSkillsMCP.Infrastructure/Cli/Commands/SearchCommand.cs` - Semantic search with --max and --temp
- `src/QdrantSkillsMCP.Infrastructure/Cli/Commands/ListCommand.cs` - List all skills
- `src/QdrantSkillsMCP.Infrastructure/Cli/Commands/LoadCommand.cs` - Load skill(s) by name
- `src/QdrantSkillsMCP.Infrastructure/Cli/Commands/CrudCommands.cs` - Add/update/delete/archive operations
- `src/QdrantSkillsMCP.Infrastructure/Cli/Commands/StatusCommand.cs` - Connection and config info
- `tests/QdrantSkillsMCP.UnitTests/Cli/ConsoleHostTests.cs` - 7 tests for subcommand dispatch
- `tests/QdrantSkillsMCP.UnitTests/Cli/ReplLoopTests.cs` - 7 tests for REPL command processing

## Decisions Made
- Used AnsiConsole.Create() with AnsiConsoleOutput(Console.Out) instead of static AnsiConsole.Write() to respect Console.SetOut redirects in tests
- Added [Collection("ConsoleOutput")] to both test classes to prevent parallel execution race conditions on Console.Out
- Extracted ReplLoop.ProcessCommandAsync as public method for unit testing without real Console.ReadKey I/O
- Used SkillParser.ToSkill() convenience method in CrudCommands instead of manual Skill construction

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Spectre.Console AnsiConsole static singleton caches Console.Out**
- **Found during:** Task 1 (GREEN phase)
- **Issue:** AnsiConsole.Write() uses a static singleton that caches the Console.Out writer at startup. When tests redirect Console.Out via SetOut, the static AnsiConsole still writes to the old (disposed) writer, causing ObjectDisposedException.
- **Fix:** Created WriteRenderable helper that instantiates a fresh IAnsiConsole per call via AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Out) })
- **Files modified:** ConsoleOutputFormatter.cs
- **Verification:** All 7 ConsoleHost tests pass
- **Committed in:** ee7ea2b (Task 1 commit)

**2. [Rule 1 - Bug] Test isolation race condition on Console.Out**
- **Found during:** Task 2 (verification)
- **Issue:** ConsoleHostTests and ReplLoopTests both redirect Console.Out. When xUnit runs them in parallel, one test can dispose the StringWriter while another test's Spectre.Console is still writing to it.
- **Fix:** Added [Collection("ConsoleOutput")] attribute to both test classes to serialize execution
- **Files modified:** ConsoleHostTests.cs, ReplLoopTests.cs
- **Verification:** All 16 CLI tests pass consistently
- **Committed in:** f57b9c9 (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (2 bugs)
**Impact on plan:** Both auto-fixes necessary for test reliability. No scope creep.

## Issues Encountered
None beyond the auto-fixed deviations above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- CLI mode complete with all subcommands and interactive REPL
- Ready for Plan 02 (setup wizard) which implements the --setup branch placeholder
- Ready for Plan 03 (bundled skill and NuGet packaging)

---
*Phase: 03-cli-distribution-and-bundled-skill*
*Completed: 2026-03-26*
