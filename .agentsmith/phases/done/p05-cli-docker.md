# Phase 5 - CLI & Docker

## Goal
Make Agent Smith deliverable as a real CLI tool and Docker image.
User should be able to run `agentsmith "fix #123 in todo-list"` - locally or in a container.

---

## Components

| Step | File | Description |
|------|------|-------------|
| 1 | `phase5-cli.md` | CLI with System.CommandLine (arguments, options, --help) |
| 2 | `phase5-docker.md` | Dockerfile (multi-stage), .dockerignore, Docker Compose example |
| 3 | `phase5-smoke-test.md` | End-to-end smoke test (DI resolution, CLI parsing) |
| 4 | Tests | CLI argument parsing, DI integration |

---

## Design Decisions

### CLI: System.CommandLine instead of hand-written parsing
- `System.CommandLine` is the official .NET CLI framework
- Gives us `--help`, `--version`, `--config`, validation for free
- Minimal overhead, no over-engineering

### Docker: Multi-Stage Build
- Stage 1: SDK for building
- Stage 2: Runtime-only image (lean)
- Config is mounted as a volume, not built in
- Target: Image < 200MB

### No Over-Engineering
- No sub-commands (only one root command)
- No interactive shell
- No watch modes or daemon processes
- Simple: Input in → PR out → Exit


---

# Phase 5 - Step 1: CLI with System.CommandLine

## Goal
Real CLI interface with argument parsing, --help, --config option.
Project: `AgentSmith.Cli/`

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
  <input>    Ticket reference and project, e.g. "fix #123 in todo-list"

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
File: src/AgentSmith.Cli/Program.cs
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
4. Output: "Would run pipeline 'fix-bug' for project 'todo-list', ticket #123"
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


---

# Phase 5 - Step 2: Docker

## Goal
Multi-stage Dockerfile for a lean runtime image.
Project root: `Dockerfile`, `.dockerignore`

---

## Dockerfile

```
File: Dockerfile (project root)
```

**Stage 1: Build**
- Base: `mcr.microsoft.com/dotnet/sdk:8.0`
- Copy solution + all csproj (for restore caching)
- `dotnet restore`
- Copy rest + `dotnet publish -c Release`

**Stage 2: Runtime**
- Base: `mcr.microsoft.com/dotnet/runtime:8.0`
- Copy published output
- Copy `config/` as default config
- ENTRYPOINT: `dotnet AgentSmith.Cli.dll`

---

## .dockerignore

```
File: .dockerignore (project root)
```

Exclude:
- `bin/`, `obj/`, `.git/`, `node_modules/`
- `*.md` (except config/coding-principles.md)
- `.vs/`, `.idea/`, `*.user`
- `tests/` (not in the runtime image)

---

## Docker Compose (Example)

```
File: docker-compose.yml (project root)
```

For local development / demo:
```yaml
services:
  agentsmith:
    build: .
    environment:
      - GITHUB_TOKEN=${GITHUB_TOKEN}
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
      - AZURE_DEVOPS_TOKEN=${AZURE_DEVOPS_TOKEN}
    volumes:
      - ./config:/app/config
      - ~/.ssh:/root/.ssh:ro
```

---

## Image Size

Target: < 200MB
- Runtime base: ~85MB
- Published app: ~30-50MB (self-contained would be ~80MB, but framework-dependent is sufficient)
- Total: ~120-150MB

---

## Testing

```bash
# Build
docker build -t agentsmith .

# Show help
docker run --rm agentsmith --help

# Dry run
docker run --rm \
  -v $(pwd)/config:/app/config \
  agentsmith --dry-run "fix #123 in todo-list"

# Real run
docker run --rm \
  -e GITHUB_TOKEN=ghp_xxx \
  -e ANTHROPIC_API_KEY=sk-xxx \
  -v $(pwd)/config:/app/config \
  agentsmith "fix #123 in todo-list"
```


---

# Phase 5 - Step 3: Smoke Tests

## Goal
Ensure the DI container is built correctly and CLI parsing works.
No real API calls, only structural validation.

---

## DI Integration Test

```
File: tests/AgentSmith.Tests/Integration/DiRegistrationTests.cs
```

Test builds the complete DI container and verifies that all services are resolvable:

```csharp
[Fact]
public void AllServices_Resolvable()
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddAgentSmithInfrastructure();
    services.AddAgentSmithCommands();
    var provider = services.BuildServiceProvider();

    // Resolve all critical services
    provider.GetRequiredService<ProcessTicketUseCase>();
    provider.GetRequiredService<ICommandExecutor>();
    provider.GetRequiredService<IIntentParser>();
    provider.GetRequiredService<IPipelineExecutor>();
    provider.GetRequiredService<IConfigurationLoader>();
    provider.GetRequiredService<ITicketProviderFactory>();
    provider.GetRequiredService<ISourceProviderFactory>();
    provider.GetRequiredService<IAgentProviderFactory>();
}
```

---

## CLI Smoke Test

Verifies only the CLI argument structure:
- `--help` outputs help text and exits 0
- Without arguments outputs error and exits 1
- `--dry-run` with valid config parses intent without pipeline execution

---

## What is NOT Tested

- Real API calls (GitHub, Azure DevOps, Anthropic)
- Real Git operations
- Docker build (that is a CI/CD concern)
