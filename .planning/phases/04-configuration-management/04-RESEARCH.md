# Phase 4: Configuration Management - Research

**Researched:** 2026-03-27
**Domain:** .NET configuration layering, CLI config UX, cross-platform shell detection
**Confidence:** HIGH

## Summary

Phase 4 adds user-facing configuration management on top of the existing `QdrantSkillsOptions` infrastructure. The core challenge is implementing a layered config system (defaults -> user -> project -> env) with read/write capability, profile support, and source annotations -- all using .NET's built-in `Microsoft.Extensions.Configuration` providers and `System.Text.Json.Nodes` for file manipulation.

The project already has the key building blocks: `QdrantSkillsOptions` defines all configurable properties, `Program.cs` demonstrates mode branching (`--console`, `--setup`, now `--config`), `JsonConfigWriterBase` provides a proven backup/merge/validate pattern for JSON files, and `Spectre.Console` is available for interactive prompts. The primary new work is: (1) a `ConfigManager` service that reads/writes layered JSON config with profile awareness, (2) a `ConfigCommand` dispatcher following the `SetupWizard` pattern, (3) shell-detection and env var template generation, and (4) a `validate` command that health-checks Qdrant + embedding provider connectivity.

**Primary recommendation:** Build a `ConfigManager` class using `System.Text.Json.Nodes.JsonNode` for read-modify-write (same as `JsonConfigWriterBase`), layer config sources in `Program.cs` via `builder.Configuration.AddJsonFile()`, and follow the `SetupWizard` pattern for the `--config` branch with both interactive and subcommand modes.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Both interactive wizard (--config with no args) and get/set CLI (--config show/set/get/reset/init/validate) -- follows the --setup pattern
- Operations: show (display all config with sources), set, get, validate (test Qdrant connection + embedding provider), reset (key to default or all), init (generate starter config)
- Default write scope is user-level (~/.qdrant-skills/config.json); use --project flag for project-level (qdrant-skills.json)
- Secrets (API keys) masked by default in --config show output (sk-****7f3a); --reveal flag shows full values
- User-level config: ~/.qdrant-skills/config.json (same directory as FrequentSkills)
- Project-level config: qdrant-skills.json (unchanged from current)
- Precedence: Environment variables > Project config > User config > Defaults
- --config show displays source annotation per value: [default], [user], [project], or [env:QDRANT_SKILLS__*]
- --config env generates a copy-pasteable shell snippet with all configurable env vars as a commented template
- Current values filled in where set; user uncomments what they need
- Auto-detect shell (bash/zsh via $SHELL, PowerShell via $PSVersionTable, fallback to bash)
- Output matching format: export for bash/zsh, $env: for PowerShell, set for cmd
- Covers all var groups: Qdrant connection, embedding provider, Azure OpenAI
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

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CFG-01 | --config branch in Program.cs with subcommand dispatch | SetupWizard pattern for mode branching; ConsoleHost pattern for subcommand dispatch |
| CFG-02 | show operation: display all config with source annotations | Microsoft.Extensions.Configuration provider chain inspection; custom source tracking |
| CFG-03 | set operation: write key=value to user or project config | System.Text.Json.Nodes read-modify-write (JsonConfigWriterBase pattern) |
| CFG-04 | get operation: read single key from resolved config | IConfiguration GetValue or direct JsonNode read |
| CFG-05 | validate operation: test Qdrant + embedding connectivity | QdrantClient health check + embedding test request |
| CFG-06 | reset operation: remove key or reset to defaults | JsonNode manipulation to remove properties |
| CFG-07 | init operation: generate starter config with local preset | Write default config.json with local profile |
| CFG-08 | Interactive wizard mode (--config with no args) | Spectre.Console prompts (SelectionPrompt, TextPrompt) |
| CFG-09 | Named profiles in config.json with use/switch | JSON profile sections; active profile tracking |
| CFG-10 | Env var helper: shell detection + template generation | Environment variable inspection; cross-platform shell detection |
| CFG-11 | Secret masking in show output with --reveal flag | String masking utility for API keys |
| CFG-12 | User-level config source added to builder.Configuration | AddJsonFile for ~/.qdrant-skills/config.json in Program.cs |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.Extensions.Configuration.Json | 10.* | JSON config file provider | Already used transitively via Hosting; AddJsonFile for layered sources |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 10.* | Env var config provider | Already used; QDRANT_SKILLS__ prefix binding |
| System.Text.Json.Nodes | (built-in) | Read-modify-write JSON files | Already proven in JsonConfigWriterBase; JsonNode/JsonObject for mutation |
| Spectre.Console | 0.54.0 | Interactive prompts and formatted output | Already used in SetupWizard; SelectionPrompt, TextPrompt, Tables |
| Qdrant.Client | 1.17.0 | Health check for validate command | Already a dependency; use HealthCheckAsync or ListCollectionsAsync |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.Extensions.Options | 10.* | IOptions binding | Already wired; for reading resolved config in validate |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| System.Text.Json.Nodes | Newtonsoft.Json | No benefit -- STJ already proven in codebase, no new dependency needed |
| Custom arg parsing | System.CommandLine | Overkill -- only 7 subcommands, SetupWizard pattern works fine |

**Installation:**
No new packages required. All dependencies are already in the project.

## Architecture Patterns

### Recommended Project Structure
```
src/QdrantSkillsMCP.Infrastructure/
├── Configuration/
│   ├── QdrantSkillsOptions.cs        # Existing - no changes needed
│   ├── EmbeddingProviderType.cs       # Existing
│   ├── ConfigManager.cs              # NEW - read/write/profile logic
│   ├── ConfigSourceTracker.cs        # NEW - tracks which source provided each value
│   └── ShellDetector.cs              # NEW - cross-platform shell detection
├── Cli/
│   ├── Commands/
│   │   └── ConfigCommand.cs          # NEW - --config subcommand dispatcher
│   └── ConsoleHost.cs                # Existing
└── Program.cs                         # Modified - add --config branch + user config source
```

### Pattern 1: Config File Format with Profiles
**What:** JSON structure for ~/.qdrant-skills/config.json supporting named profiles
**When to use:** All config read/write operations

Recommended format (flat keys matching QdrantSkillsOptions property names under "QdrantSkills" section):
```json
{
  "activeProfile": "local",
  "profiles": {
    "local": {
      "QdrantSkills": {
        "QdrantHost": "localhost",
        "QdrantGrpcPort": 6334,
        "CollectionName": "skills",
        "EmbeddingProvider": "LocalONNX"
      }
    },
    "cloud": {
      "QdrantSkills": {
        "QdrantHost": "my-qdrant.cloud.example.com",
        "QdrantGrpcPort": 6334,
        "QdrantApiKey": "sk-abc123...",
        "CollectionName": "skills",
        "EmbeddingProvider": "OpenAI",
        "OpenAiApiKey": "sk-xyz789..."
      }
    }
  }
}
```

**Rationale:** Nesting under "QdrantSkills" matches the existing `QdrantSkillsOptions.SectionName` so `IConfiguration.GetSection("QdrantSkills")` works directly. The `profiles` wrapper keeps profiles separate from active-profile metadata. Flat property names inside `QdrantSkills` match property names 1:1.

### Pattern 2: Mode Branch in Program.cs
**What:** Add --config as a peer to --console and --setup
**When to use:** Program.cs entry point
```csharp
// In Program.cs, add before or alongside --console/--setup:
if (args.Contains("--config"))
{
    // Config mode: no Qdrant connection needed for most ops
    var builder = Host.CreateApplicationBuilder(args);
    AddUserConfig(builder.Configuration);
    builder.Configuration.AddJsonFile("qdrant-skills.json", optional: true, reloadOnChange: false);
    // Register only config services (lightweight)
    builder.Services.AddSingleton<ConfigManager>();

    var host = builder.Build();
    var configManager = host.Services.GetRequiredService<ConfigManager>();
    var exitCode = await ConfigCommand.RunAsync(configManager, args);
    Environment.Exit(exitCode);
}
```

### Pattern 3: Source Annotation Tracking
**What:** Track which config source provided each value for --config show
**When to use:** show operation

The approach: read each layer independently (defaults, user file, project file, env vars) and compare to determine source. Do NOT rely on IConfigurationRoot.Providers enumeration (fragile). Instead:
1. Load defaults from `new QdrantSkillsOptions()` (constructor defaults)
2. Load user config JSON directly via `JsonNode.Parse()`
3. Load project config JSON directly via `JsonNode.Parse()`
4. Check `Environment.GetEnvironmentVariable("QDRANT_SKILLS__*")` for each key
5. Compare resolved value against each layer to determine source

### Pattern 4: Config-Mode DI (Lightweight)
**What:** --config mode registers minimal services, not full Qdrant infrastructure
**When to use:** Most config operations don't need Qdrant

Follow the `AddSetupServices()` pattern: separate DI registration for config mode. Only `validate` needs Qdrant + embedding connections -- and those can be built ad-hoc from the resolved config rather than through full DI.

### Anti-Patterns to Avoid
- **Don't use IConfigurationRoot.Providers for source tracking:** Provider enumeration is implementation-dependent and doesn't reliably tell you which provider "won" for each key. Read each layer independently instead.
- **Don't modify QdrantSkillsOptions for profiles:** Profiles are a config-file concern, not an options-class concern. The active profile's values simply become the QdrantSkills section at config load time.
- **Don't register full Qdrant infrastructure for --config mode:** Follow the --setup pattern of lightweight DI. Only validate needs connectivity.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON read-modify-write | Custom file manipulation | System.Text.Json.Nodes (JsonNode.Parse, ToJsonString) + JsonConfigWriterBase backup pattern | Proven in codebase; handles edge cases (empty files, invalid JSON) |
| Config layering | Custom precedence logic | Microsoft.Extensions.Configuration provider chain | AddJsonFile order determines precedence; last wins |
| Interactive prompts | Console.ReadLine loops | Spectre.Console SelectionPrompt/TextPrompt | Already used in SetupWizard; handles validation, escaping, terminal capabilities |
| Secret masking | Regex replacement | Simple string helper: `value[..3] + "****" + value[^4..]` | Deterministic, testable, no regex overhead |
| Env var prefix stripping | Manual string ops | `QDRANT_SKILLS__` prefix is already handled by .NET config provider | Just document the mapping |

**Key insight:** This phase is primarily UX on top of existing infrastructure. The config binding, options pattern, and JSON manipulation are all proven in the codebase.

## Common Pitfalls

### Pitfall 1: Config Source Order Matters
**What goes wrong:** User config overrides project config (wrong precedence)
**Why it happens:** `AddJsonFile` uses last-wins semantics in Microsoft.Extensions.Configuration
**How to avoid:** Add sources in precedence order: appsettings.json (defaults) -> user config -> project config -> env vars. Later sources override earlier ones.
**Warning signs:** Values in qdrant-skills.json being ignored when ~/.qdrant-skills/config.json has different values

### Pitfall 2: Profile Resolution at Config Load Time
**What goes wrong:** Changing active profile requires app restart
**Why it happens:** IConfiguration is built once at startup
**How to avoid:** For --config mode, this is fine (it's a CLI tool, not a long-running server). For MCP/console mode, the active profile is resolved once at startup -- document this behavior. Profile switching via `--config use` changes the file for next startup.

### Pitfall 3: Cross-Platform Path Handling
**What goes wrong:** `~` not expanded on Windows, path separators wrong
**Why it happens:** `~` is a shell expansion, not a .NET concept
**How to avoid:** Always use `Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)` + `Path.Combine()`. FrequentSkillsService already does this correctly -- follow that pattern.
**Warning signs:** FileNotFoundException on Windows with paths containing `~`

### Pitfall 4: Shell Detection Edge Cases
**What goes wrong:** Wrong shell format on Windows (PowerShell vs cmd vs WSL bash)
**Why it happens:** Windows users may run from cmd.exe, PowerShell, or WSL
**How to avoid:** Check `PSModulePath` env var (present in PowerShell), `SHELL` env var (present in Unix/WSL), `ComSpec` (cmd.exe). Default to PowerShell on Windows if ambiguous (most common for .NET developers).
**Warning signs:** `export` statements shown to PowerShell users

### Pitfall 5: Concurrent Config File Writes
**What goes wrong:** Two processes write config simultaneously, one overwrites the other
**Why it happens:** CLI tool might be invoked in parallel
**How to avoid:** JsonConfigWriterBase already creates .bak files. For config writes, this is sufficient -- config changes are infrequent user-initiated operations, not high-frequency concurrent writes.

### Pitfall 6: QdrantClient TLS for Remote Hosts
**What goes wrong:** Connection fails to remote Qdrant Cloud because TLS not enabled
**Why it happens:** Default QdrantClient constructor uses plaintext gRPC (no TLS)
**How to avoid:** For non-localhost hosts, QdrantClient needs HTTPS channel. Use `QdrantChannel.ForAddress("https://host:port", new ClientConfiguration { ApiKey = key })` + `QdrantGrpcClient` constructor. The validate command should detect non-localhost and warn about TLS. Consider adding a `UseTls` boolean to QdrantSkillsOptions.
**Warning signs:** gRPC connection timeouts to remote hosts

## Code Examples

### Config File Read-Modify-Write (following JsonConfigWriterBase pattern)
```csharp
// Source: Existing JsonConfigWriterBase pattern in codebase
public async Task SetValueAsync(string key, string value, bool projectScope)
{
    var path = projectScope
        ? Path.Combine(Directory.GetCurrentDirectory(), "qdrant-skills.json")
        : GetUserConfigPath();

    // Backup
    if (File.Exists(path))
        File.Copy(path, path + ".bak", overwrite: true);

    // Read or create
    JsonNode root;
    if (File.Exists(path))
    {
        var json = await File.ReadAllTextAsync(path);
        root = JsonNode.Parse(json) ?? new JsonObject();
    }
    else
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        root = new JsonObject();
    }

    // For user config with profiles, navigate to active profile section
    // For project config, write directly under "QdrantSkills"
    var section = GetOrCreateSection(root, "QdrantSkills");
    section[key] = value;

    await File.WriteAllTextAsync(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
}
```

### Shell Detection
```csharp
public static ShellType DetectShell()
{
    // PowerShell: PSModulePath is always set
    if (Environment.GetEnvironmentVariable("PSModulePath") is not null)
        return ShellType.PowerShell;

    // Unix shells
    var shell = Environment.GetEnvironmentVariable("SHELL");
    if (shell is not null)
    {
        if (shell.EndsWith("/zsh")) return ShellType.Zsh;
        if (shell.EndsWith("/bash")) return ShellType.Bash;
        return ShellType.Bash; // fallback for other Unix shells
    }

    // Windows without PowerShell = cmd
    if (OperatingSystem.IsWindows())
        return ShellType.Cmd;

    return ShellType.Bash; // ultimate fallback
}
```

### Env Var Template Generation
```csharp
// Bash/Zsh format
// export QDRANT_SKILLS__QdrantHost="localhost"
// export QDRANT_SKILLS__QdrantGrpcPort="6334"
// # export QDRANT_SKILLS__QdrantApiKey=""

// PowerShell format
// $env:QDRANT_SKILLS__QdrantHost = "localhost"
// $env:QDRANT_SKILLS__QdrantGrpcPort = "6334"
// # $env:QDRANT_SKILLS__QdrantApiKey = ""

// Cmd format
// set QDRANT_SKILLS__QdrantHost=localhost
// set QDRANT_SKILLS__QdrantGrpcPort=6334
// REM set QDRANT_SKILLS__QdrantApiKey=
```

### Secret Masking
```csharp
public static string MaskSecret(string? value)
{
    if (string.IsNullOrEmpty(value)) return "(not set)";
    if (value.Length <= 8) return "****";
    return value[..3] + "****" + value[^4..];
}
// "sk-abc123def456" -> "sk-****f456"
```

### Validate Command Flow
```csharp
// 1. Show resolved config summary
// 2. Test Qdrant connection (ListCollectionsAsync with timeout)
// 3. Test embedding provider (generate test embedding)
// 4. Report pass/fail per check

// For TLS detection on validate:
if (host != "localhost" && host != "127.0.0.1" && !useTls)
{
    AnsiConsole.MarkupLine("[yellow]WARNING:[/] Non-localhost host detected without TLS.");
    AnsiConsole.MarkupLine("[yellow]Remote Qdrant instances typically require HTTPS.[/]");
}
```

### Adding User Config to Program.cs
```csharp
// Add user-level config source BEFORE project-level (project overrides user)
static void AddUserConfig(IConfigurationBuilder config)
{
    var userConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".qdrant-skills",
        "config.json");

    if (File.Exists(userConfigPath))
    {
        // Read active profile, extract its QdrantSkills section, add as in-memory source
        // OR: use a custom config provider that resolves profiles
        config.AddJsonFile(userConfigPath, optional: true, reloadOnChange: false);
    }
}
```

**Note on profile-aware loading:** The user config file has profiles. At load time, read the `activeProfile` field, extract that profile's `QdrantSkills` section, and add it as an in-memory configuration source. This avoids the complexity of a custom IConfigurationProvider.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Manual appsettings.json editing | CLI config commands | This phase | Users don't need to know JSON structure |
| Single flat config file | Layered: user + project + env | This phase | Per-environment flexibility |
| Hardcoded localhost | Named profiles with presets | This phase | Easy environment switching |

**Existing patterns preserved:**
- `IOptions<QdrantSkillsOptions>` binding stays unchanged
- `qdrant-skills.json` project config stays unchanged
- `QDRANT_SKILLS__` env var prefix stays unchanged
- All existing config still works; Phase 4 adds new UX on top

## Open Questions

1. **Profile-aware config loading in MCP/console mode**
   - What we know: --config mode can read the active profile at command time
   - What's unclear: How to load the active profile's config into `builder.Configuration` for MCP mode without a custom IConfigurationProvider
   - Recommendation: Read `activeProfile` from user config, extract that profile's values, add as `AddInMemoryCollection()` source. Simple, no custom provider needed.

2. **UseTls property on QdrantSkillsOptions**
   - What we know: Remote Qdrant hosts need HTTPS; current QdrantClient constructor uses plaintext
   - What's unclear: Whether to add an explicit `UseTls` boolean or auto-detect from host
   - Recommendation: Add `UseTls` boolean (default false). Auto-detection is a convenience hint in `validate`, not a silent override. Users explicitly enable TLS in their config.

3. **Config key naming: property names vs friendly names**
   - What we know: QdrantSkillsOptions uses PascalCase property names (QdrantHost, QdrantGrpcPort)
   - What's unclear: Whether --config set/get should use these exact names or friendlier aliases
   - Recommendation: Use exact property names for set/get (simple, unambiguous). Show with friendly descriptions alongside.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 (3.2.2) with MTP |
| Config file | tests/QdrantSkillsMCP.UnitTests/QdrantSkillsMCP.UnitTests.csproj |
| Quick run command | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~Config" -x` |
| Full suite command | `dotnet test` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CFG-01 | --config branch dispatches to subcommands | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ConfigCommand" -x` | Wave 0 |
| CFG-02 | show displays config with source annotations | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ConfigShow" -x` | Wave 0 |
| CFG-03 | set writes key=value to correct file | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ConfigSet" -x` | Wave 0 |
| CFG-04 | get reads resolved value | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ConfigGet" -x` | Wave 0 |
| CFG-05 | validate tests Qdrant + embedding health | integration | `dotnet test tests/QdrantSkillsMCP.IntegrationTests --filter "FullyQualifiedName~ConfigValidate" -x` | Wave 0 |
| CFG-06 | reset removes key or resets all | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ConfigReset" -x` | Wave 0 |
| CFG-07 | init creates starter config with local preset | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ConfigInit" -x` | Wave 0 |
| CFG-08 | Interactive wizard prompts for config values | manual-only | N/A (requires terminal interaction) | N/A |
| CFG-09 | Named profiles: create, switch, list | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ConfigProfile" -x` | Wave 0 |
| CFG-10 | Env var helper detects shell and generates template | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ShellDetect" -x` | Wave 0 |
| CFG-11 | Secret masking with --reveal override | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~SecretMask" -x` | Wave 0 |
| CFG-12 | User config source integrated in builder | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~UserConfig" -x` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~Config" -x`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before /gsd:verify-work

### Wave 0 Gaps
- [ ] `tests/QdrantSkillsMCP.UnitTests/Config/ConfigManagerTests.cs` -- covers CFG-02 through CFG-07, CFG-09
- [ ] `tests/QdrantSkillsMCP.UnitTests/Config/ShellDetectorTests.cs` -- covers CFG-10
- [ ] `tests/QdrantSkillsMCP.UnitTests/Config/SecretMaskTests.cs` -- covers CFG-11
- [ ] `tests/QdrantSkillsMCP.UnitTests/Config/ConfigCommandTests.cs` -- covers CFG-01
- [ ] ConfigManager tests should use temp directories (constructor-injected paths like FrequentSkillsService)

## Sources

### Primary (HIGH confidence)
- Existing codebase: QdrantSkillsOptions.cs, Program.cs, ServiceRegistration.cs, JsonConfigWriterBase.cs, SetupWizard.cs, FrequentSkillsService.cs -- all read directly
- Microsoft.Extensions.Configuration -- AddJsonFile layering semantics (built-in .NET, well-known)

### Secondary (MEDIUM confidence)
- [Qdrant .NET SDK](https://github.com/qdrant/qdrant-dotnet) -- TLS via QdrantChannel.ForAddress with HTTPS scheme
- [Spectre.Console](https://spectreconsole.net/) -- already in use, version 0.54.0

### Tertiary (LOW confidence)
- Shell detection heuristics (PSModulePath for PowerShell, $SHELL for Unix) -- well-established conventions but edge cases exist on exotic setups

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all libraries already in project, no new dependencies
- Architecture: HIGH -- follows established patterns (JsonConfigWriterBase, SetupWizard, ConsoleHost)
- Pitfalls: HIGH -- most are standard .NET configuration gotchas well-documented in the ecosystem
- Shell detection: MEDIUM -- cross-platform shell detection has edge cases on Windows with WSL

**Research date:** 2026-03-27
**Valid until:** 2026-04-27 (stable domain, no fast-moving dependencies)
