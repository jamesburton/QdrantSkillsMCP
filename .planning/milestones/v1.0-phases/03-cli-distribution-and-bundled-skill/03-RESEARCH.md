# Phase 3: CLI, Distribution, and Bundled Skill - Research

**Researched:** 2026-03-26
**Domain:** .NET CLI tooling, NuGet tool packaging, agent MCP configuration, REPL implementation
**Confidence:** HIGH

## Summary

Phase 3 adds three capabilities to the existing QdrantSkillsMCP server: (1) a `--console` CLI mode with single-shot subcommands and interactive REPL, (2) a `--setup` wizard that detects installed AI agents and writes MCP config entries, and (3) a bundled SKILL.md that teaches agents how to use the server. The existing codebase already has all business logic in DI-injected services (ISkillRepository, IEmbeddingService, ISessionTracker) and MCP tool classes (SkillSearchTools, SkillCrudTools, SessionTools), so the CLI layer is a thin adapter that resolves the same services from the DI container and formats output for humans.

The NuGet tool packaging (`PackAsTool`) is straightforward -- three MSBuild properties in the existing Infrastructure.csproj. The `dnx` command in .NET 10 SDK runs NuGet tools without permanent installation, which is the intended invocation model. The setup wizard must support 7+ agents with different config file formats (JSON, TOML, YAML), so a provider pattern with per-agent config writers is the cleanest approach. The REPL needs readline-style editing with tab completion for skill names -- ReadLine (tonerdo) or a manual implementation with Console.ReadKey are the two viable paths.

**Primary recommendation:** Build the CLI as a thin layer over existing DI services. Use args-based branching in Program.cs before MCP server setup. Implement per-agent config writers behind a common interface for the setup wizard.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Human-readable output by default; `--json` flag switches to JSON for scripting/agent consumption
- Console subcommands: `search`, `list`, `load`, `add`, `update`, `delete`, `archive`, `status`/`info`
- `--console` with no subcommand enters interactive REPL with command history and tab completion
- Mode branching in Program.cs: check for `--console` and `--setup` early, route to CLI handler (no MCP server startup)
- Single entry point, single project (Infrastructure)
- Setup wizard: config file probing for known filesystem paths per agent
- Supported agents: Claude, Copilot, Codex, opencode, docker-agent, kilocode, factory-droid (and others)
- Always backup existing config to `.bak` before modifying
- Merge QdrantSkillsMCP entry into agent's existing config; fallback to snippets for unknown formats
- Non-interactive mode: `--setup --agent claude --level user`
- Interactive flow: auto-detect agents, user confirms/deselects, configure all selected
- SKILL.md: embedded resource, placed by `--setup`; also exposed via `get-skill-guide` MCP tool
- Bootstrap skill (`enable-skill-search`) as minimal entry point
- Dual-file frequent skills: `FrequentSkills.md` (shared) + `FrequentSkills.local.md` (personal, gitignored)
- Two-tier location: user-level (`~/.qdrant-skills/`) and project-level (project root)
- NuGet tool: PackageId `QdrantSkillsMCP`, ToolCommandName `qdrant-skills-mcp`, invocation `dnx qdrant-skills-mcp`
- Portable / framework-dependent (requires .NET 10 runtime)
- Separate companion NuGet package for ONNX model: `QdrantSkillsMCP.Models.DefaultEmbedding`

### Claude's Discretion
- REPL library choice (Spectre.Console, ReadLine, or custom)
- Tab completion implementation for skill names in REPL
- Exact config file paths for each supported agent
- JSON merge strategy for agent config files
- Bootstrap skill (`enable-skill-search`) exact content and placement
- Status/info command output format and fields

### Deferred Ideas (OUT OF SCOPE)
- Frequent skills sync to Qdrant or shared repo
- skills-guru integration (ECO-01, ECO-02)
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CLI-01 | `--console` flag enables CLI mode with single-shot subcommands and JSON output | Args branching pattern in Program.cs; existing tool classes provide all logic |
| CLI-02 | `--console` without subcommand enters interactive REPL mode | ReadLine or manual Console.ReadKey REPL with tab completion; IAutoCompleteHandler for skill names |
| CLI-03 | `--setup` auto-configures MCP server entry in agent config files | Per-agent config writer pattern; agent config paths documented below |
| CLI-04 | `--setup` supports claude, copilot, codex, opencode, docker-agent, kilocode, factory-droid | Agent config format matrix documented in Architecture Patterns section |
| CLI-05 | `--setup` auto-writes config where possible, falls back to snippets | JSON/TOML merge strategies; snippet generation for unknown formats |
| CLI-06 | `--setup` supports project-level and user-level configuration | Agent scope mapping documented per agent |
| CLI-07 | `--setup` interactive if no params, accepts args for non-interactive | Interactive: detect + list + confirm; non-interactive: `--agent` + `--level` flags |
| DIST-01 | Packaged as NuGet tool via `dnx QdrantSkillsMCP` | PackAsTool + ToolCommandName MSBuild properties; dnx runs without install |
| BSKL-01 | Ships SKILL.md teaching agents how to use QdrantSkillsMCP | Embedded resource in Infrastructure.csproj; get-skill-guide MCP tool |
| BSKL-02 | Bundled skill includes curated short-list of frequently used skills | Dual-file FrequentSkills.md system; merge order: user -> project -> project-local |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.CommandLine (manual) | N/A | CLI arg parsing via manual string matching | Project uses simple args-based branching, no framework needed for `--console`/`--setup` flags |
| System.Text.Json | 10.x (in-box) | JSON config file reading/writing and CLI JSON output | Already used in MCP tool classes; consistent serialization |
| Microsoft.Extensions.Hosting | 10.x | DI container and configuration for CLI mode | Already registered via `AddQdrantSkillsInfrastructure()` |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Spectre.Console | 0.54.0 | Rich console output (tables, colors, prompts) | Human-readable CLI output formatting, interactive agent selection |
| Tomlyn | 0.17+ | TOML parsing/writing for Codex config | Setup wizard writing to `~/.codex/config.toml` |

### REPL Library Recommendation (Claude's Discretion)

**Recommendation: Manual Console.ReadKey loop with custom tab completion.**

Rationale:
- ReadLine (tonerdo) and ReadLine.Reboot are unmaintained or low-activity (last meaningful updates 2020-2023)
- Spectre.Console's TextPrompt does not support readline-style line editing with tab completion in a loop
- A custom REPL loop (~100-150 lines) using `Console.ReadKey(intercept: true)` gives full control over:
  - Tab completion cycling through skill names (fetched from ISkillRepository on REPL start)
  - Command history with up/down arrows (simple `List<string>` ring buffer)
  - Line editing (backspace, home, end, left, right)
- This avoids a dependency on a potentially stale library and keeps the REPL behavior exactly as specified

If the team prefers a library, **ReadLine 2.0.1** (tonerdo original, MIT license) is the simplest drop-in with `IAutoCompleteHandler` for tab completion. It is old but stable and has no dependencies.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Manual REPL | ReadLine 2.0.1 | Simpler code but unmaintained (2020); no .NET 10 testing |
| Manual REPL | Spectre.Console prompts | No readline-style editing; prompts are one-shot, not REPL |
| Tomlyn | Manual TOML string generation | Codex config is simple enough for string templates; loses roundtrip fidelity |
| Spectre.Console | Plain Console.Write | Spectre gives tables, colors, progress for free; worth the dependency |

**Installation:**
```bash
dotnet add src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj package Spectre.Console --version 0.54.0
dotnet add src/QdrantSkillsMCP.Infrastructure/QdrantSkillsMCP.Infrastructure.csproj package Tomlyn --version 0.17.0
```

## Architecture Patterns

### Recommended Project Structure
```
src/QdrantSkillsMCP.Infrastructure/
├── Cli/
│   ├── ConsoleHost.cs           # CLI entry point: routes subcommands to handlers
│   ├── ConsoleOutputFormatter.cs # Human-readable vs JSON output formatting
│   ├── ReplLoop.cs              # Interactive REPL with tab completion
│   └── Commands/
│       ├── SearchCommand.cs     # Calls ISkillRepository + IEmbeddingService
│       ├── ListCommand.cs       # Calls ISkillRepository
│       ├── LoadCommand.cs       # Calls ISkillRepository
│       ├── CrudCommands.cs      # Add/Update/Delete/Archive
│       └── StatusCommand.cs     # Connection info, collection stats
├── Setup/
│   ├── SetupWizard.cs           # Interactive flow: detect, select, configure
│   ├── AgentDetector.cs         # Probes filesystem for installed agents
│   ├── IAgentConfigWriter.cs    # Interface for per-agent config writing
│   └── Writers/
│       ├── ClaudeConfigWriter.cs
│       ├── CopilotConfigWriter.cs
│       ├── CodexConfigWriter.cs
│       ├── OpenCodeConfigWriter.cs
│       ├── KiloCodeConfigWriter.cs
│       ├── FactoryDroidConfigWriter.cs
│       ├── VsCodeConfigWriter.cs
│       └── SnippetFallbackWriter.cs  # Prints copy-paste snippet
├── Skill/
│   ├── SKILL.md                 # Embedded resource
│   ├── FrequentSkills.md        # Default frequent skills template
│   └── EnableSkillSearch.md     # Bootstrap skill content
├── Tools/
│   └── SkillGuideTools.cs       # get-skill-guide MCP tool (returns SKILL.md content)
└── Program.cs                   # Mode branching: --console, --setup, or MCP server
```

### Pattern 1: Args-Based Mode Branching
**What:** Check CLI args before building the host to determine execution mode
**When to use:** Always -- this is the entry point logic

```csharp
// Program.cs -- mode branching
var builder = Host.CreateApplicationBuilder(args);

// Register infrastructure services (shared between all modes)
builder.Services.AddQdrantSkillsInfrastructure(builder.Configuration);

if (args.Contains("--console"))
{
    // CLI mode: no MCP server, no stdio transport
    // Relax logging (stdout is safe in CLI mode)
    var host = builder.Build();
    var consoleHost = new ConsoleHost(host.Services);
    return await consoleHost.RunAsync(args);
}
else if (args.Contains("--setup"))
{
    var host = builder.Build();
    var wizard = host.Services.GetRequiredService<SetupWizard>();
    return await wizard.RunAsync(args);
}
else
{
    // Default: MCP server mode (existing behavior)
    builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();
    await builder.Build().RunAsync();
    return 0;
}
```

### Pattern 2: Agent Config Writer Interface
**What:** Each agent gets a dedicated config writer implementing a common interface
**When to use:** Setup wizard writing MCP config entries

```csharp
public interface IAgentConfigWriter
{
    string AgentName { get; }
    bool CanAutoWrite { get; }
    AgentScope[] SupportedScopes { get; }
    string? DetectInstallation(); // Returns config path or null
    Task WriteConfigAsync(string configPath, AgentScope scope, McpServerEntry entry);
    string GenerateSnippet(McpServerEntry entry, AgentScope scope); // Fallback
}

public record McpServerEntry(string ServerName, string Command, string[] Args);
public enum AgentScope { User, Project }
```

### Pattern 3: CLI Command Dispatch
**What:** Parse subcommand from args and dispatch to handler using existing DI services
**When to use:** `--console search "query"`, `--console list`, etc.

```csharp
public class ConsoleHost(IServiceProvider services)
{
    public async Task<int> RunAsync(string[] args)
    {
        // Strip --console from args
        var commandArgs = args.Where(a => a != "--console" && a != "--json").ToArray();
        var jsonOutput = args.Contains("--json");
        var formatter = new ConsoleOutputFormatter(jsonOutput);

        if (commandArgs.Length == 0)
        {
            // No subcommand: enter REPL
            var repl = new ReplLoop(services, formatter);
            return await repl.RunAsync();
        }

        var command = commandArgs[0].ToLowerInvariant();
        return command switch
        {
            "search" => await HandleSearch(commandArgs[1..], formatter),
            "list" => await HandleList(formatter),
            "load" => await HandleLoad(commandArgs[1..], formatter),
            // ... etc
        };
    }
}
```

### Pattern 4: Embedded Resource for SKILL.md
**What:** Bundle SKILL.md as an embedded resource in the assembly
**When to use:** SKILL.md content served by MCP tool and written by setup wizard

```xml
<!-- In Infrastructure.csproj -->
<ItemGroup>
    <EmbeddedResource Include="Skill\SKILL.md" />
    <EmbeddedResource Include="Skill\FrequentSkills.md" />
    <EmbeddedResource Include="Skill\EnableSkillSearch.md" />
</ItemGroup>
```

```csharp
// Reading embedded resource
var assembly = typeof(ConsoleHost).Assembly;
using var stream = assembly.GetManifestResourceStream(
    "QdrantSkillsMCP.Infrastructure.Skill.SKILL.md");
using var reader = new StreamReader(stream!);
return await reader.ReadToEndAsync();
```

### Anti-Patterns to Avoid
- **Rewriting business logic in CLI commands:** All logic already exists in service interfaces. CLI commands should resolve services from DI and call them, not duplicate logic.
- **Mixing stdout between MCP and CLI:** In MCP mode, stdout is reserved for JSON-RPC. The mode branching MUST prevent MCP transport registration in CLI mode.
- **Monolithic setup wizard:** Don't put all agent config formats in one giant switch statement. Use the IAgentConfigWriter pattern for extensibility.
- **Hard-coding config paths:** Agent config paths vary by OS. Use `Environment.GetFolderPath()` and `Environment.GetEnvironmentVariable()` for cross-platform paths.

## Agent Config File Matrix

This is the critical reference for the setup wizard (CLI-03 through CLI-07).

| Agent | Format | User-Level Path | Project-Level Path | Config Structure |
|-------|--------|-----------------|-------------------|------------------|
| Claude Code | JSON | `~/.claude.json` (under `mcpServers`) | `.mcp.json` (under `mcpServers`) | `{"mcpServers":{"name":{"command":"...","args":[...]}}}` |
| Claude Desktop | JSON | `~/AppData/Roaming/Claude/claude_desktop_config.json` (Windows) / `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) | N/A | `{"mcpServers":{"name":{"command":"...","args":[...]}}}` |
| VS Code / Copilot | JSON | User `mcp.json` (via MCP: Open User Configuration) | `.vscode/mcp.json` | `{"servers":{"name":{"command":"...","args":[...]}}}` (note: `servers` not `mcpServers`) |
| Copilot CLI | JSON | `~/.copilot/mcp-config.json` | N/A | `{"mcpServers":{"name":{"type":"local","command":"...","args":[...]}}}` |
| OpenAI Codex | TOML | `~/.codex/config.toml` | `.codex/config.toml` | `[mcp_servers.name]\ncommand = "..."\nargs = [...]` |
| opencode | JSON | `~/.config/opencode/opencode.json` | `opencode.json` (project root) | `{"mcp":{"name":{"type":"local","command":[...]}}}` (note: command is array) |
| kilocode | JSON | Global `mcp_settings.json` | `.kilocode/mcp.json` | `{"mcpServers":{"name":{"command":"...","args":[...]}}}` |
| factory-droid | JSON | `~/.factory/mcp.json` | `.factory/mcp.json` | `{"mcpServers":{"name":{"type":"stdio","command":"...","args":[...]}}}` |

**The MCP server entry for QdrantSkillsMCP:**
```json
{
  "command": "dnx",
  "args": ["qdrant-skills-mcp"]
}
```

**Key differences to handle:**
1. VS Code uses `"servers"` instead of `"mcpServers"` as the root key
2. OpenCode uses `"mcp"` as root key and `"command"` is an array, not a string
3. Codex uses TOML format, not JSON
4. Some agents require `"type": "stdio"` or `"type": "local"` explicitly
5. Claude Desktop path varies by OS

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON config merge | Custom JSON tree merge | `JsonNode` (System.Text.Json) | `JsonNode` supports mutable read-modify-write of JSON documents without deserializing to typed objects |
| TOML config merge | Custom TOML parser | Tomlyn library | TOML has subtle syntax rules (inline tables, arrays of tables) that are easy to get wrong |
| Console tables | Manual string padding | Spectre.Console `Table` | Handles column width, Unicode, wrapping automatically |
| Interactive prompts | Custom selection UI | Spectre.Console `SelectionPrompt` / `MultiSelectionPrompt` | Arrow-key selection, search filtering built in |
| Config file backup | Manual File.Copy | `File.Copy(path, path + ".bak", overwrite: true)` | Simple but MUST happen before any write -- wrap in a helper that throws if backup fails |

**Key insight:** The setup wizard's config writing is the most complex part of this phase. Each agent has a slightly different JSON structure, and some use TOML. The IAgentConfigWriter pattern isolates each format behind a clean interface, so adding a new agent is just adding a new writer class.

## Common Pitfalls

### Pitfall 1: Stdout Pollution in MCP Mode
**What goes wrong:** CLI output accidentally written to stdout when in MCP mode, breaking JSON-RPC transport
**Why it happens:** Shared code paths between CLI and MCP modes
**How to avoid:** Mode branching in Program.cs MUST happen before any output. CLI mode should NOT register MCP server/transport. The existing `LogToStandardErrorThreshold = LogLevel.Trace` setting handles MCP mode logging.
**Warning signs:** MCP client disconnects or receives malformed JSON

### Pitfall 2: Config File Corruption
**What goes wrong:** Partial write to agent config file leaves it in invalid state
**Why it happens:** Write interrupted, or JSON/TOML merge logic has bug
**How to avoid:** Always backup to `.bak` first. Write to a temp file, then atomic rename (or File.Replace on Windows). Validate the written file by parsing it back.
**Warning signs:** Agent can't start after setup

### Pitfall 3: Cross-Platform Path Handling
**What goes wrong:** Hardcoded paths like `~/.claude.json` don't expand on Windows
**Why it happens:** `~` is a shell expansion, not a .NET concept
**How to avoid:** Use `Environment.GetFolderPath(SpecialFolder.UserProfile)` for `~`, `Environment.GetFolderPath(SpecialFolder.ApplicationData)` for `%APPDATA%`. Always use `Path.Combine()` for path construction.
**Warning signs:** "File not found" on Windows when path works on macOS/Linux

### Pitfall 4: DI Hosted Services Running in CLI Mode
**What goes wrong:** `DimensionValidator` and `CollectionInitializer` IHostedService start running during CLI setup wizard, attempting Qdrant connection
**Why it happens:** `builder.Build()` starts all hosted services
**How to avoid:** For `--setup` mode, DON'T register infrastructure services (skip `AddQdrantSkillsInfrastructure`). For `--console` mode, services are needed but should handle missing Qdrant gracefully.
**Warning signs:** Setup wizard hangs or throws connecting to Qdrant when user just wants to configure agents

### Pitfall 5: REPL Exit Handling
**What goes wrong:** Ctrl+C kills the process without cleanup
**Why it happens:** Default `Console.CancelKeyPress` behavior terminates
**How to avoid:** Register `Console.CancelKeyPress` handler that sets a cancellation flag. REPL loop checks the flag each iteration.
**Warning signs:** Process crashes with unhandled exception on Ctrl+C

### Pitfall 6: Embedded Resource Naming
**What goes wrong:** `GetManifestResourceStream` returns null
**Why it happens:** Resource name includes namespace + folder path with dots, which is easy to get wrong
**How to avoid:** Use `assembly.GetManifestResourceNames()` during development to list actual names. Convention: `{DefaultNamespace}.{Folder}.{FileName}` with dots replacing path separators.
**Warning signs:** NullReferenceException when accessing SKILL.md content

## Code Examples

### NuGet Tool Packaging (DIST-01)
```xml
<!-- Infrastructure.csproj additions -->
<PropertyGroup>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>qdrant-skills-mcp</ToolCommandName>
    <PackageId>QdrantSkillsMCP</PackageId>
    <Description>MCP server for semantic skill search and retrieval using Qdrant</Description>
    <PackageOutputPath>./nupkg</PackageOutputPath>
</PropertyGroup>
```
Source: [Microsoft Learn - Create a .NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create)

### JSON Config Merge Using JsonNode
```csharp
// Read existing config, merge in new MCP server entry
using System.Text.Json.Nodes;

public static async Task MergeJsonConfigAsync(
    string configPath, string rootKey, string serverName, JsonObject serverConfig)
{
    JsonNode root;
    if (File.Exists(configPath))
    {
        var json = await File.ReadAllTextAsync(configPath);
        root = JsonNode.Parse(json) ?? new JsonObject();
    }
    else
    {
        root = new JsonObject();
    }

    var serversNode = root[rootKey]?.AsObject();
    if (serversNode is null)
    {
        serversNode = new JsonObject();
        root.AsObject()[rootKey] = serversNode;
    }

    serversNode[serverName] = serverConfig;

    var options = new JsonSerializerOptions { WriteIndented = true };
    await File.WriteAllTextAsync(configPath, root.ToJsonString(options));
}
```

### REPL Loop with Tab Completion
```csharp
public class ReplLoop
{
    private readonly List<string> _history = new();
    private string[] _skillNames = Array.Empty<string>();

    public async Task RunAsync()
    {
        // Pre-fetch skill names for tab completion
        var repo = _services.GetRequiredService<ISkillRepository>();
        var skills = await repo.ListAsync(CancellationToken.None);
        _skillNames = skills.Select(s => s.Name).ToArray();

        Console.WriteLine("QdrantSkillsMCP REPL. Type 'help' for commands, 'exit' to quit.");

        while (true)
        {
            Console.Write("> ");
            var line = ReadLineWithCompletion();
            if (line is null || line is "exit" or "quit") break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            _history.Add(line);
            await ExecuteCommandAsync(line);
        }
    }
}
```

### Agent Detection
```csharp
public record DetectedAgent(string Name, string ConfigPath, AgentScope Scope);

public class AgentDetector
{
    private static readonly string Home = Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile);

    public IReadOnlyList<DetectedAgent> DetectInstalledAgents()
    {
        var agents = new List<DetectedAgent>();

        // Claude Code
        var claudeJson = Path.Combine(Home, ".claude.json");
        if (File.Exists(claudeJson))
            agents.Add(new("Claude Code", claudeJson, AgentScope.User));

        // Copilot CLI
        var copilotConfig = Path.Combine(Home, ".copilot", "mcp-config.json");
        if (File.Exists(copilotConfig) || Directory.Exists(Path.Combine(Home, ".copilot")))
            agents.Add(new("Copilot CLI", copilotConfig, AgentScope.User));

        // Codex
        var codexConfig = Path.Combine(Home, ".codex", "config.toml");
        if (File.Exists(codexConfig) || Directory.Exists(Path.Combine(Home, ".codex")))
            agents.Add(new("OpenAI Codex", codexConfig, AgentScope.User));

        // opencode
        var opencodePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "opencode", "opencode.json");
        // Linux: ~/.config/opencode/opencode.json
        if (File.Exists(opencodePath))
            agents.Add(new("opencode", opencodePath, AgentScope.User));

        // kilocode
        var kilocodePath = Path.Combine(Home, ".kilocode", "mcp.json"); // approximate
        if (Directory.Exists(Path.Combine(Home, ".kilocode")))
            agents.Add(new("kilocode", kilocodePath, AgentScope.User));

        // factory-droid
        var factoryPath = Path.Combine(Home, ".factory", "mcp.json");
        if (File.Exists(factoryPath) || Directory.Exists(Path.Combine(Home, ".factory")))
            agents.Add(new("factory-droid", factoryPath, AgentScope.User));

        return agents;
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `dotnet tool install -g` then run | `dnx <tool>` runs without install | .NET 10 Preview 6 (2025) | Users don't need to globally install the tool |
| `dotnet tool run` (local tools) | `dnx` auto-downloads from NuGet | .NET 10 Preview 6 (2025) | Zero-install experience for MCP servers |
| Separate CLI project | `PackAsTool` on existing exe project | Stable since .NET Core 3.0 | Single project serves both MCP and CLI modes |
| System.CommandLine (beta) | Manual args parsing for simple CLIs | Ongoing | System.CommandLine still not 1.0; manual parsing fine for `--console`/`--setup` |

**Deprecated/outdated:**
- `dnx` in .NET 10 replaces the need for `dotnet tool install -g` for one-shot usage
- System.CommandLine remains in beta/preview; not worth adding as dependency for two flags

## Open Questions

1. **Kilocode global config path**
   - What we know: Project-level is `.kilocode/mcp.json`. Global might be `mcp_settings.json` somewhere.
   - What's unclear: Exact global config file path on each OS
   - Recommendation: Detect by checking `~/.kilocode/` directory existence; print snippet if global path uncertain

2. **Docker Agent (cagent) config format**
   - What we know: Docker agent uses YAML-based agent definitions. MCP can be configured through docker CLI.
   - What's unclear: Whether there's a standard user-level MCP config file path for docker-agent specifically
   - Recommendation: Use snippet fallback for docker-agent initially; add auto-write when format stabilizes

3. **opencode config path on Windows**
   - What we know: Linux uses `~/.config/opencode/opencode.json`
   - What's unclear: Whether Windows uses `%APPDATA%/opencode/opencode.json` or follows XDG
   - Recommendation: Check both `%APPDATA%/opencode/` and `%LOCALAPPDATA%/opencode/` on Windows

4. **ONNX companion package content**
   - What we know: Decision is to create `QdrantSkillsMCP.Models.DefaultEmbedding` as separate NuGet package
   - What's unclear: How to package raw ONNX model files in a NuGet content package that a tool can discover at runtime
   - Recommendation: Use `contentFiles` in the companion .nuspec with a well-known path convention; main tool probes NuGet global packages folder

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 + NSubstitute 5.x |
| Config file | Tests already configured in UnitTests.csproj and IntegrationTests.csproj |
| Quick run command | `dotnet test tests/QdrantSkillsMCP.UnitTests --no-build -x` |
| Full suite command | `dotnet test --no-build` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CLI-01 | `--console search` returns results | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ConsoleHost" -x` | Wave 0 |
| CLI-02 | `--console` with no subcommand starts REPL (smoke) | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ReplLoop" -x` | Wave 0 |
| CLI-03 | `--setup` writes valid Claude config | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ConfigWriter" -x` | Wave 0 |
| CLI-04 | Agent detection finds installed agents | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~AgentDetector" -x` | Wave 0 |
| CLI-05 | Snippet fallback for unknown agents | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~SnippetFallback" -x` | Wave 0 |
| CLI-06 | Project vs user scope config paths | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~ConfigWriter" -x` | Wave 0 |
| CLI-07 | Non-interactive args parsing | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~SetupWizard" -x` | Wave 0 |
| DIST-01 | `dotnet pack` produces valid tool package | integration | `dotnet pack src/QdrantSkillsMCP.Infrastructure -c Release && dotnet tool install --global --add-source ./nupkg QdrantSkillsMCP` | manual |
| BSKL-01 | SKILL.md accessible as embedded resource | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~SkillGuide" -x` | Wave 0 |
| BSKL-02 | FrequentSkills merge order correct | unit | `dotnet test tests/QdrantSkillsMCP.UnitTests --filter "FullyQualifiedName~FrequentSkills" -x` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/QdrantSkillsMCP.UnitTests --no-build -x`
- **Per wave merge:** `dotnet test --no-build`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/QdrantSkillsMCP.UnitTests/Cli/ConsoleHostTests.cs` -- covers CLI-01, CLI-02
- [ ] `tests/QdrantSkillsMCP.UnitTests/Cli/ReplLoopTests.cs` -- covers CLI-02
- [ ] `tests/QdrantSkillsMCP.UnitTests/Setup/AgentDetectorTests.cs` -- covers CLI-04
- [ ] `tests/QdrantSkillsMCP.UnitTests/Setup/ConfigWriterTests.cs` -- covers CLI-03, CLI-05, CLI-06
- [ ] `tests/QdrantSkillsMCP.UnitTests/Setup/SetupWizardTests.cs` -- covers CLI-07
- [ ] `tests/QdrantSkillsMCP.UnitTests/Skill/SkillGuideTests.cs` -- covers BSKL-01
- [ ] `tests/QdrantSkillsMCP.UnitTests/Skill/FrequentSkillsTests.cs` -- covers BSKL-02

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn - Create a .NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create) - PackAsTool, ToolCommandName properties
- [Microsoft Learn - dotnet tool exec](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-exec) - dnx command behavior
- [Andrew Lock - Running one-off .NET tools with dnx](https://andrewlock.net/exploring-dotnet-10-preview-features-5-running-one-off-dotnet-tools-with-dnx/) - dnx internals
- [Claude Code MCP docs](https://code.claude.com/docs/en/mcp) - Claude Code config format, scopes, .mcp.json structure
- [VS Code MCP docs](https://code.visualstudio.com/docs/copilot/customization/mcp-servers) - VS Code/Copilot mcp.json format
- [OpenAI Codex MCP docs](https://developers.openai.com/codex/mcp) - Codex TOML config format
- [GitHub Copilot CLI MCP docs](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-mcp-servers) - ~/.copilot/mcp-config.json format
- [Factory.ai MCP docs](https://docs.factory.ai/cli/configuration/mcp) - .factory/mcp.json format
- [Kilo Code MCP docs](https://kilo.ai/docs/features/mcp/overview) - .kilocode/mcp.json format

### Secondary (MEDIUM confidence)
- [Spectre.Console NuGet](https://www.nuget.org/packages/spectre.console) - Version 0.54.0 confirmed
- [Tomlyn NuGet](https://www.nuget.org/packages/Tomlyn/) - TOML parser for .NET
- [OpenCode MCP docs](https://opencode.ai/docs/mcp-servers/) - opencode.json format (fetched, verified structure)

### Tertiary (LOW confidence)
- Docker agent MCP config path -- could not verify exact user-level config file path (docs are sparse)
- Kilocode global config path -- web search suggests `mcp_settings.json` but exact location unconfirmed
- opencode Windows path -- unverified whether it uses %APPDATA% or XDG convention

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - PackAsTool, Spectre.Console, JsonNode all well-documented and stable
- Architecture: HIGH - Mode branching pattern is straightforward; existing DI services provide all logic
- Agent config formats: MEDIUM-HIGH - 5 of 7 agents have verified config formats; 2 have uncertain paths
- Pitfalls: HIGH - Based on direct analysis of existing codebase and known .NET patterns
- REPL implementation: MEDIUM - Custom approach recommended but untested; ReadLine library alternative available

**Research date:** 2026-03-26
**Valid until:** 2026-04-26 (agent config paths may change; core .NET patterns stable)
