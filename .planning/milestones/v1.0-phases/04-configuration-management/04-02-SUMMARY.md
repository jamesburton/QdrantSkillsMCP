---
phase: 04-configuration-management
plan: 02
subsystem: cli
tags: [config, spectre-console, qdrant-client, tls]

requires:
  - phase: 04-01
    provides: ConfigManager, ShellDetector, SecretMask
provides:
  - ConfigCommand dispatcher with 8 subcommands
  - Interactive config wizard
  - Validate command with TLS auto-detection
  - Program.cs --config branch with user-level config loading
affects: [all modes that connect to Qdrant]

tech-stack:
  added: []
  patterns: [config command dispatch via switch expression, UserConfigLoader for config source chain]

key-files:
  created:
    - src/QdrantSkillsMCP.Infrastructure/Cli/Commands/ConfigCommand.cs
    - src/QdrantSkillsMCP.Infrastructure/Configuration/UserConfigLoader.cs
    - tests/QdrantSkillsMCP.UnitTests/Config/ConfigCommandTests.cs
    - tests/QdrantSkillsMCP.UnitTests/Config/UserConfigWiringTests.cs
    - tests/QdrantSkillsMCP.IntegrationTests/Config/ConfigValidateTests.cs
  modified:
    - src/QdrantSkillsMCP.Infrastructure/Program.cs
    - src/QdrantSkillsMCP.Infrastructure/ServiceRegistration.cs
    - src/QdrantSkillsMCP.Infrastructure/Configuration/QdrantSkillsOptions.cs

key-decisions:
  - "UseTls property added to QdrantSkillsOptions, wired into QdrantClient construction"
  - "Auto-enable TLS for remote hosts in validate command"
  - "Wizard Set flow shows current value as default, examples per key, empty to skip"
  - "HTTP_1_1_REQUIRED hint for Azure-proxied Qdrant instances"

patterns-established:
  - "Config command dispatch: switch expression in RunAsync routes to subcommand methods"
  - "UserConfigLoader: adds ~/.qdrant-skills/config.json as config source with profile awareness"

requirements-completed: [CFG-01, CFG-05, CFG-08, CFG-12]

duration: 8min
completed: 2026-03-28
---

# Plan 04-02: ConfigCommand + Wizard + Validate + Wiring Summary

**ConfigCommand dispatcher routes 8 subcommands with interactive wizard fallback, TLS-aware validate, and user-level config source integration.**

## What Changed

1. **ConfigCommand.cs** — Dispatches show/set/get/init/reset/use/env/validate subcommands. Interactive wizard with Spectre.Console when no subcommand. Set flow shows current values, examples, allows skip.

2. **Program.cs** — New `--config` branch loads user config via UserConfigLoader, creates ConfigManager, dispatches to ConfigCommand.

3. **Validate command** — Auto-enables TLS for remote hosts. Helpful error hints for HTTP_1_1_REQUIRED (Azure proxy) and missing TLS configuration.

4. **QdrantSkillsOptions.UseTls** — New boolean property wired into QdrantClient construction in ServiceRegistration.cs.

5. **Tests** — 13 ConfigCommand unit tests, 5 UserConfig wiring tests, 2 ConfigValidate integration tests.

## Deviations

- Added UseTls support beyond original plan scope (discovered during manual testing with Azure Qdrant instance)
- Wizard UX improvements (defaults, examples, skip) added based on user feedback during checkpoint

## Self-Check: PASSED

All config tests pass (89/89). Build succeeds with 0 errors.
