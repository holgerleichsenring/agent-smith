# Phase 21: Code Quality & Structure Refactoring

## Context

A full code audit revealed ~100 violations against the coding principles: oversized classes/methods, inconsistent folder structure, missing `sealed` keywords, magic numbers, missing tests, and more. This phase brings the entire codebase in line with the coding principles and establishes a clear, consistent folder structure convention.

## Goal

1. Update CODING-PRINCIPLES.md with explicit folder structure rules
2. Restructure all 6 projects to follow the new convention
3. Fix all code quality violations (oversized classes, magic numbers, naming, etc.)
4. Improve test coverage for critical untested components

The guiding principle: **content coherence over mechanical line counting**. A 25-line method that does one thing well stays. A 40-line method doing 3 things gets split.

---

## New Folder Structure Convention

Top-level folders per project (only these are allowed at root):

| Folder | Contains |
|--------|----------|
| `Contracts/` | Interfaces (with contextual sub-dirs like `Providers/`, `Slack/`) |
| `Models/` | Records, DTOs, data classes, configuration models |
| `Entities/` | Domain entities (Domain project only) |
| `Services/` | All functional code (with sub-dirs: `Handlers/`, `Factories/`, `Providers/`, `Bus/`) |
| `Extensions/` | Extension method classes |
| `Exceptions/` | Custom exception classes |

Rules:
- Factories, Configuration loaders, Handlers are part of `Services/` — not root-level
- Interfaces live in the `AgentSmith.Contracts` project (cross-layer) or in a local `Contracts/` folder (project-internal)
- One type per file, file-scoped namespaces, `sealed` on all non-base classes

---

## Target Structure per Project

### AgentSmith.Domain
```
Models/        ← was ValueObjects/ (BranchName, CommandResult, FilePath, ProjectName, TicketId)
Entities/      ← unchanged (Ticket, Repository, Plan, PlanStep, CodeChange, CodeAnalysis)
Exceptions/    ← unchanged
```

### AgentSmith.Contracts
```
Providers/     ← IAgentProvider, ISourceProvider, ITicketProvider, factories, IModelRegistry, TaskType
Services/      ← ICommandContextFactory, IConfigurationLoader, IIntentParser, IPipelineExecutor, IProgressReporter
Commands/      ← ICommandContext, ICommandHandler, ICommandExecutor, PipelineContext, ContextKeys
Models/
  Configuration/ ← all config classes (AgentSmithConfig, ProjectConfig, RetryConfig, etc.)
  ParsedIntent.cs
```

### AgentSmith.Application
```
Services/
  Handlers/    ← 9 command handlers
  CommandExecutor.cs
  CommandContextFactory.cs
  PipelineExecutor.cs
  ConsoleProgressReporter.cs
  RegexIntentParser.cs
  ProcessTicketUseCase.cs
Models/        ← 9 command context records
Extensions/    ← ServiceCollectionExtensions.cs
```

### AgentSmith.Infrastructure
```
Services/
  Providers/
    Agent/     ← ClaudeAgentProvider, OpenAiAgentProvider, GeminiAgentProvider, loops, scout, tools, compactor, etc.
    Source/     ← GitHub, GitLab, Azure, Local source providers
    Tickets/   ← Azure DevOps, GitHub, GitLab, Jira ticket providers
  Factories/   ← AgentProviderFactory, SourceProviderFactory, TicketProviderFactory
  Configuration/ ← YamlConfigurationLoader, SecretsProvider
  Bus/         ← RedisMessageBus, RedisProgressReporter
Models/        ← BusMessage, IMessageBus, ScoutResult, RunCostSummary, TokenUsageSummary, ToolDefinitions
Extensions/    ← ServiceCollectionExtensions.cs
```

### AgentSmith.Dispatcher
```
Contracts/     ← IPlatformAdapter, IJobSpawner, IHaikuIntentParser, IProjectResolver, IMessageBus (project-internal interfaces)
Services/
  Adapters/    ← SlackAdapter, SlackErrorBlockBuilder, SlackSignatureVerifier, etc.
  Handlers/    ← FixTicketIntentHandler, ListTicketsIntentHandler, CreateTicketIntentHandler, HelpHandler
  IntentEngine.cs, ProjectResolver.cs, ConversationStateManager.cs, MessageBusListener.cs, etc.
Models/        ← ChatIntent, ConversationState, ErrorContext, BusMessage, PendingClarification, etc.
Extensions/    ← ServiceCollectionExtensions, WebApplicationExtensions
Services/      ← DispatcherBanner, DispatcherDefaults, ErrorFormatter, etc.
```

### AgentSmith.Host
```
Program.cs
Services/      ← WebhookListener
```

---

## Phase 21 Steps

| Step | File | Description |
|------|------|-------------|
| 21-1 | `phase21-principles.md` | Update CODING-PRINCIPLES.md with folder structure rules |
| 21-2 | `phase21-structure-domain.md` | Rename ValueObjects/ → Models/, update namespaces |
| 21-3 | `phase21-structure-contracts.md` | Move Configuration/ → Models/Configuration/, move ParsedIntent |
| 21-4 | `phase21-structure-application.md` | Merge Commands/+UseCases/ into Services/+Models/, move Extensions/ |
| 21-5 | `phase21-structure-infrastructure.md` | Move Factories/, Configuration/, Bus/ under Services/, extract Models/ |
| 21-6 | `phase21-structure-dispatcher.md` | Extract Contracts/, move root files, reorganize Services/ |
| 21-7 | `phase21-refactor-classes.md` | Break up oversized classes for content coherence |
| 21-8 | `phase21-code-quality.md` | Fix sealed, magic numbers, boolean naming, Console.Write, empty catch, HttpClient |
| 21-9 | `phase21-tests.md` | Add tests for untested handlers, providers, and Dispatcher |

---

## Step 21-1: Update CODING-PRINCIPLES.md

Add new section **"Project Structure"** after "Hard Limits":

```markdown
## Project Structure

Each project follows a consistent top-level folder convention:

- `Contracts/` — Interfaces (with contextual sub-directories like `Providers/`, `Slack/`)
- `Models/` — Records, DTOs, configuration classes, data objects
- `Entities/` — Domain entities (Domain project only)
- `Services/` — All functional code (handlers, factories, providers, configuration loaders, bus)
- `Extensions/` — Extension method classes (`ServiceCollectionExtensions`, etc.)
- `Exceptions/` — Custom exception classes

Rules:
- Factories, Handlers, and Configuration loaders live under `Services/` — never at root level.
- Cross-layer interfaces belong in `AgentSmith.Contracts`. Project-internal interfaces use a local `Contracts/` folder.
- No loose files at project root (except `Program.cs` in Host/Dispatcher).
```

---

## Steps 21-2 through 21-6: Folder Restructuring

Each step involves:
1. Create target folders
2. Move files with `git mv`
3. Update namespace declarations in moved files
4. Update all `using` statements across the solution
5. Run `dotnet build` to verify

### Namespace Changes Summary

| Old Namespace | New Namespace |
|---------------|---------------|
| `AgentSmith.Domain.ValueObjects` | `AgentSmith.Domain.Models` |
| `AgentSmith.Contracts.Configuration` | `AgentSmith.Contracts.Models.Configuration` |
| `AgentSmith.Application.Commands.Contexts` | `AgentSmith.Application.Models` |
| `AgentSmith.Application.Commands.Handlers` | `AgentSmith.Application.Services.Handlers` |
| `AgentSmith.Application.Commands` (CommandExecutor) | `AgentSmith.Application.Services` |
| `AgentSmith.Application.UseCases` | `AgentSmith.Application.Services` |
| `AgentSmith.Infrastructure.Factories` | `AgentSmith.Infrastructure.Services.Factories` |
| `AgentSmith.Infrastructure.Configuration` | `AgentSmith.Infrastructure.Services.Configuration` |
| `AgentSmith.Infrastructure.Bus` | `AgentSmith.Infrastructure.Services.Bus` |

---

## Step 21-7: Refactor Oversized Classes

Focus on **content coherence** — split only where a class does multiple unrelated things.

### ClaudeAgentProvider (373 lines → ~120 + helpers)
- Extract `AgentPromptBuilder` — shared prompt building logic (also used by OpenAI/Gemini)
- Extract `AgentExecutionHelper` — common execution flow (create ToolExecutor, run loop, track costs)
- Provider keeps only orchestration: `GeneratePlanAsync`, `ExecutePlanAsync`

### ToolExecutor (211 lines → ~120 + command runner)
- Extract `CommandRunner` — the `RunCommand` method (43 lines) with process management
- Extract `FileOperations` — `ReadFile` (34 lines) with encoding detection

### ScoutAgent (145 lines, DiscoverAsync = 87 lines)
- Extract prompt building into a private helper
- Split discovery loop from result parsing

### ClaudeContextCompactor (190 lines, CompactAsync = 63 lines)
- Extract `ExtractTextContent` into a static helper class
- Split `CompactAsync` into strategy selection + execution

### SlackAdapter (301 lines)
- `SlackAdapterOptions` already needs its own file (multiple types per file)
- Extract `PostAsync` HTTP helper into base or utility

### DockerJobSpawner (195 lines, SpawnAsync = 63 lines)
- Extract `BuildContainerConfig` and `BuildEnv` into focused methods

### MessageBusListener (255 lines, HandleMessageAsync = 45 lines)
- Extract message routing into `RouteMessageAsync` helper methods

### PipelineExecutor (129 lines, ExecuteAsync = 51 lines)
- Extract command dispatch into smaller methods per command type

### Program.cs Host (216 lines)
- Extract `BuildServiceProvider` and banner into separate classes

---

## Step 21-8: Code Quality Fixes

### 8a: Add `sealed` to 14 config classes
All classes in `Contracts/Models/Configuration/`: `AgentSmithConfig`, `ProjectConfig`, `PipelineConfig`, `AgentConfig`, `SourceConfig`, `TicketConfig`, `RetryConfig`, `CacheConfig`, `CompactionConfig`, `ModelRegistryConfig`, `ModelAssignment`, `PricingConfig`, `ModelPricing`

### 8b: One type per file
- `PricingConfig.cs` → extract `ModelPricing` to its own `ModelPricing.cs`

### 8c: Boolean naming
- `CommandResult.Success` → `CommandResult.IsSuccess` (14 references in 7 files)
- `CompactionConfig.Enabled` → `CompactionConfig.IsEnabled` (4 references in 2 files)
- `CacheConfig.Enabled` → `CacheConfig.IsEnabled` (4 references in 2 files)

### 8d: Magic numbers → constants
- `MaxTokens = 8192` → `AgentConstants.DefaultMaxTokens`
- `MaxTokens = 2048` → `AgentConstants.CompactionMaxTokens`
- `.Take(200)` → `AgentConstants.MaxFileLinesToInclude`
- `Length > 2000` → `AgentConstants.MaxTextContentLength`
- `0.25` jitter → `RetryConstants.JitterFactor`
- `"https://gitlab.com"` → `ProviderDefaults.GitLabBaseUrl`

### 8e: Console.WriteLine → ILogger
- `Program.cs` Host: banner stays (intentional CLI output), but result output uses ILogger
- `DispatcherBanner.cs`: banner stays (intentional startup output)
- `ApprovalHandler.cs`: stays (intentional interactive CLI prompt)
- `ConsoleProgressReporter.cs`: stays (intentional CLI output)

### 8f: Empty catch block
- `ClaudeAgentProvider.cs` line 144: add `logger.LogDebug` before swallowing

### 8g: `new HttpClient()` → IHttpClientFactory
- `SourceProviderFactory`: inject `IHttpClientFactory`, use `CreateClient()`
- `TicketProviderFactory`: inject `IHttpClientFactory`, use `CreateClient()`

---

## Step 21-9: Add Missing Tests

Priority targets (highest impact, most testable):

### Command Handlers (6 missing)
- `FetchTicketHandlerTests` — mock ITicketProviderFactory
- `CheckoutSourceHandlerTests` — mock ISourceProviderFactory
- `GeneratePlanHandlerTests` — mock IAgentProviderFactory
- `AgenticExecuteHandlerTests` — mock IAgentProviderFactory
- `TestHandlerTests` — test command detection and timeout
- `ApprovalHandlerTests` — test headless auto-approve

### Infrastructure (key classes)
- `ErrorFormatterTests` — pure function, easy to test
- `SecretsProviderTests` — env var resolution

### Dispatcher (key services)
- `IntentEngineTests` — regex + fallback flow
- `ProjectResolverTests` — single/multi project resolution
- `ConversationStateManagerTests` — state lifecycle
- `SlackErrorBlockBuilderTests` — block kit output

---

## Success Criteria

- [ ] `dotnet build` succeeds with zero warnings for all projects
- [ ] `dotnet test` passes all existing + new tests
- [ ] No folder at project root level other than: Contracts/, Models/, Entities/, Services/, Extensions/, Exceptions/ (plus Program.cs)
- [ ] CODING-PRINCIPLES.md contains the new folder structure section
- [ ] All config classes have `sealed` keyword
- [ ] No magic numbers — all extracted to named constants
- [ ] `CommandResult.IsSuccess` replaces `.Success` everywhere
- [ ] No `new HttpClient()` in factories
- [ ] No empty catch blocks
- [ ] At least 12 new test classes added
- [ ] All namespaces match folder structure

## Dependencies

- No new NuGet packages required
- All changes are internal refactoring — no API/behavior changes
- Existing tests must continue to pass after namespace changes

## Execution Order

Steps 21-1 through 21-6 must be sequential (each build on previous namespace changes).
Steps 21-7 and 21-8 can be interleaved.
Step 21-9 (tests) runs last, after all structural changes are stable.
