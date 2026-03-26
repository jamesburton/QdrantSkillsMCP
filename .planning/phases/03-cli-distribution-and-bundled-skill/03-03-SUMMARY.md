---
phase: 03-cli-distribution-and-bundled-skill
plan: 03
subsystem: skill-guide
tags: [skill-md, frequent-skills, mcp-tool, nuget-packaging, embedded-resource, bootstrap-skill]

# Dependency graph
requires:
  - phase: 01-core-mcp-server
    provides: ISkillRepository, MCP tool pattern
  - plan: 03-01
    provides: Console CLI mode and NuGet tool properties
  - plan: 03-02
    provides: Setup wizard with SKILL.md placement
provides:
  - "SKILL.md agent teaching guide as embedded resource"
  - "get-skill-guide MCP tool for on-demand skill guide retrieval"
  - "FrequentSkillsService with 4-tier merge (user shared/local, project shared/local)"
  - "EnableSkillSearch bootstrap skill as embedded resource"
  - "Valid NuGet tool package (QdrantSkillsMCP.1.0.0.nupkg)"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns: [SkillGuide folder to avoid Skill namespace conflict with Core.Models.Skill, embedded resource for .md files]

key-files:
  created:
    - src/QdrantSkillsMCP.Infrastructure/SkillGuide/SKILL.md
    - src/QdrantSkillsMCP.Infrastructure/SkillGuide/FrequentSkills.md
    - src/QdrantSkillsMCP.Infrastructure/SkillGuide/EnableSkillSearch.md
    - src/QdrantSkillsMCP.Infrastructure/SkillGuide/FrequentSkillsService.cs
    - src/QdrantSkillsMCP.Infrastructure/Tools/SkillGuideTools.cs
    - tests/QdrantSkillsMCP.UnitTests/SkillGuide/SkillGuideTests.cs
    - tests/QdrantSkillsMCP.UnitTests/SkillGuide/FrequentSkillsTests.cs
  modified:
    - src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj
    - src/QdrantSkillsMCP.Infrastructure/Setup/SetupWizard.cs

key-decisions:
  - "SkillGuide folder name instead of Skill to avoid namespace conflict with QdrantSkillsMCP.Core.Models.Skill"
  - "FrequentSkillsService uses constructor-injected userDir for testability (defaults to ~/.qdrant-skills/)"
  - "Embedded resources use SkillGuide subfolder naming: QdrantSkillsMCP.Infrastructure.SkillGuide.SKILL.md"
  - "get-skill-guide is ReadOnly MCP tool with [Description] attribute (matching existing tool pattern)"

patterns-established:
  - "Embedded .md files as EmbeddedResource in csproj, read via Assembly.GetManifestResourceStream"
  - "4-tier FrequentSkills merge: user shared -> user local -> project shared -> project local"

requirements-completed: [DIST-01, BSKL-01, BSKL-02]

# Metrics
duration: 10min
completed: 2026-03-26
---

# Phase 3 Plan 3: Bundled Skill and NuGet Packaging Summary

**SKILL.md agent teaching guide with search-before-load pattern, FrequentSkills 4-tier merge system, get-skill-guide MCP tool, and verified NuGet tool package**

## Performance

- **Duration:** 10 min
- **Started:** 2026-03-26T01:07:01Z
- **Completed:** 2026-03-26T01:17:00Z
- **Tasks:** 2
- **Files modified:** 9

## Accomplishments
- SKILL.md comprehensive agent teaching guide covering all 9 MCP tools, search-before-load pattern, output modes, session tracking, and frequent skills
- FrequentSkillsService with 4-tier merge system (user shared, user local, project shared, project local)
- get-skill-guide MCP tool returns embedded SKILL.md content on demand
- EnableSkillSearch bootstrap skill for agent onboarding
- FrequentSkills.md template for team-curated skill lists
- NuGet tool packaging verified: QdrantSkillsMCP.1.0.0.nupkg with embedded resources and DotnetToolSettings.xml
- 12 new unit tests (5 embedded resource + 7 merge logic), all 173 total tests pass

## Task Commits

Each task was committed atomically:

1. **Task 1: SKILL.md, FrequentSkills system, and get-skill-guide MCP tool** - `acd0abb` (feat)
2. **Task 2: NuGet tool packaging and full build verification** - verification only, no source changes

_TDD flow: tests written first (RED), then implementation (GREEN) for Task 1._

## Files Created/Modified
- `src/.../SkillGuide/SKILL.md` - Agent teaching guide (85 lines) covering tools, patterns, best practices
- `src/.../SkillGuide/FrequentSkills.md` - Default template for team-curated frequent skills
- `src/.../SkillGuide/EnableSkillSearch.md` - Bootstrap skill for agent onboarding
- `src/.../SkillGuide/FrequentSkillsService.cs` - 4-tier merge logic with file-based skill name parsing
- `src/.../Tools/SkillGuideTools.cs` - get-skill-guide MCP tool reading embedded SKILL.md
- `src/.../QdrantSkillsMCP.Infrastructure.csproj` - EmbeddedResource entries for 3 .md files
- `src/.../Setup/SetupWizard.cs` - Updated embedded resource name (Skill -> SkillGuide)
- `tests/.../SkillGuide/SkillGuideTests.cs` - 5 tests for embedded resources and MCP tool
- `tests/.../SkillGuide/FrequentSkillsTests.cs` - 7 tests for merge logic and edge cases

## Decisions Made
- Used SkillGuide folder name instead of Skill to avoid C# namespace conflict with QdrantSkillsMCP.Core.Models.Skill type
- FrequentSkillsService accepts userDir in constructor for testability (defaults to ~/.qdrant-skills/)
- get-skill-guide is a ReadOnly MCP tool following the existing [McpServerTool] + [Description] attribute pattern
- Comment-prefixed lines (<!-- -->) in FrequentSkills.md are excluded from skill name parsing

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Skill namespace conflicts with Core.Models.Skill type**
- **Found during:** Task 1 (GREEN phase build)
- **Issue:** Creating a `Skill/` folder under Infrastructure created a `QdrantSkillsMCP.Infrastructure.Skill` namespace that shadowed the `Skill` type from `QdrantSkillsMCP.Core.Models`. This broke 12+ existing files referencing the Skill model.
- **Fix:** Renamed folder from `Skill` to `SkillGuide` in both source and test projects. Updated all embedded resource names and namespace references.
- **Files modified:** All SkillGuide/ files, csproj, SetupWizard.cs
- **Commit:** acd0abb

---

**Total deviations:** 1 auto-fixed (namespace conflict)
**Impact on plan:** Folder name changed from plan's `Skill/` to `SkillGuide/`. All functionality identical.

## Issues Encountered
None beyond the auto-fixed deviation above.

## User Setup Required
None - no external service configuration required.

## Verification Results
- `dotnet build --no-restore` succeeds (both Debug and Release)
- `dotnet test --no-restore` all 173 tests pass
- `dotnet pack -c Release` produces QdrantSkillsMCP.1.0.0.nupkg (267MB, includes ONNX model)
- SKILL.md accessible via get-skill-guide MCP tool (verified by unit test)
- FrequentSkills merge order verified by 7 unit tests
- EnableSkillSearch.md embedded resource verified by unit test

---
*Phase: 03-cli-distribution-and-bundled-skill*
*Completed: 2026-03-26*
