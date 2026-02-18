# Phase 5 - Step 1: CLI with System.CommandLine

## Goal
Real CLI interface with argument parsing, --help, --config option.
Project: `AgentSmith.Host/`

---

## NuGet Package

```
System.CommandLine --version 2.0.0-beta4.22272.1
```

(Latest stable beta, widely used, also recommended by the .NET team)

---

## CLI Design

```
agentsmith <input> [options]

Arguments:
  <input>    Ticket reference and project, e.g. "fix #123 in payslip"

Options:
  --config <path>    Path to configuration file [default: config/agentsmith.yml]
  --dry-run          Parse intent and show plan, but don't execute
  --verbose          Enable verbose logging
  --version          Show version information
  --help             Show help
```

---

## Program.cs Refactoring

```
File: src/AgentSmith.Host/Program.cs
```

Instead of the simple `args[0]` parsing, `System.CommandLine` is used:

1. Root command with argument `<input>` and options
2. Handler: Build DI container → Call ProcessTicketUseCase
3. `--dry-run`: Only IntentParser + Config lookup, no pipeline execution
4. `--verbose`: Set LogLevel to Debug
5. Exit code: 0 = Success, 1 = Error

**Structure:**

```csharp
var inputArg = new Argument<string>("input", "Ticket reference and project");
var configOption = new Option<string>("--config", () => "config/agentsmith.yml", "Config file path");
var dryRunOption = new Option<bool>("--dry-run", "Parse only, don't execute");
var verboseOption = new Option<bool>("--verbose", "Enable verbose logging");

var rootCommand = new RootCommand("Agent Smith - AI Coding Agent")
{
    inputArg, configOption, dryRunOption, verboseOption
};

rootCommand.SetHandler(async (input, config, dryRun, verbose) =>
{
    // Build DI, configure logging, run use case
}, inputArg, configOption, dryRunOption, verboseOption);

return await rootCommand.InvokeAsync(args);
```

---

## Dry-Run Mode

With `--dry-run`:
1. Load config
2. Parse intent
3. Find project + pipeline
4. Output: "Would run pipeline 'fix-bug' for project 'payslip', ticket #123"
5. List pipeline commands
6. Exit 0

No API call, no checkout, no PR.

---

## Tests

**CliTests:**
- `ParseArgs_ValidInput_ReturnsZeroExitCode` (difficult without real providers)
- Better: Unit tests for DI resolution

**DI Integration Test:**
- `AllServices_Resolvable_FromContainer` - Builds the full DI container, resolves all services
