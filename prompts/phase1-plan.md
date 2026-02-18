# Phase 1: Core Infrastructure - Implementation Plan

## Goal
Foundation of the project: Solution Structure, Domain Entities, all Contracts (interfaces only), Config Loader.
After Phase 1 the solution compiles but has no functional logic.

---

## Prerequisite
- .NET 8 SDK installed
- This repo cloned

## Steps

### Step 1: Solution & Projects Setup
See: `prompts/phase1-solution-structure.md`

Create the .NET Solution with all projects and correct references.
Result: `dotnet build` succeeds (empty but error-free).

### Step 2: Domain Entities & Value Objects
See: `prompts/phase1-domain.md`

The core types of the system. Pure data models without infrastructure dependencies.
- Entities: `Ticket`, `Repository`, `Plan`, `CodeChange`, `CodeAnalysis`
- Value Objects: `TicketId`, `ProjectName`, `BranchName`, `FilePath`, `CommandResult`
- Exceptions: `AgentSmithException`, `TicketNotFoundException`, `ConfigurationException`

### Step 3: Contracts (Interfaces)
See: `prompts/phase1-contracts.md`

All interfaces that define the system. No implementation.
- Command Pattern (MediatR-Style): `ICommandContext`, `ICommandHandler<TContext>`, `ICommandExecutor`
- Shared State: `PipelineContext`, `ContextKeys`
- Providers: `ITicketProvider`, `ISourceProvider`, `IAgentProvider`
- Services: `IPipelineExecutor`, `IIntentParser`, `IConfigurationLoader`
- Factories: `ITicketProviderFactory`, `ISourceProviderFactory`, `IAgentProviderFactory`

### Step 4: Configuration
See: `prompts/phase1-config.md`

Load YAML-based configuration and provide it as strongly typed objects.
- Config Models: `AgentSmithConfig`, `ProjectConfig`, `SourceConfig`, `TicketConfig`, `AgentConfig`, `PipelineConfig`
- `YamlConfigurationLoader` implementation (only implementation in Phase 1)
- Template `agentsmith.yml` as example

### Step 5: Verify
```bash
dotnet build
dotnet test  # Empty test suite, but must pass
```

---

## Dependencies Between Steps

```
Step 1 (Solution)
    └── Step 2 (Domain)
         └── Step 3 (Contracts) ← needs Domain types
              └── Step 4 (Config) ← needs Contracts
                   └── Step 5 (Verify)
```

Strictly sequential. Each step builds on the previous one.

---

## Project References After Phase 1

```
AgentSmith.Domain          → (no dependencies)
AgentSmith.Contracts       → AgentSmith.Domain
AgentSmith.Application     → AgentSmith.Contracts, AgentSmith.Domain
AgentSmith.Infrastructure  → AgentSmith.Contracts, AgentSmith.Domain
AgentSmith.Host            → AgentSmith.Application, AgentSmith.Infrastructure
AgentSmith.Tests           → AgentSmith.Domain, AgentSmith.Contracts, AgentSmith.Infrastructure
```

---

## NuGet Packages (Phase 1)

| Project | Package | Purpose |
|---------|---------|---------|
| AgentSmith.Infrastructure | YamlDotNet | Load YAML config |
| AgentSmith.Host | Microsoft.Extensions.DependencyInjection | DI Container |
| AgentSmith.Host | Microsoft.Extensions.Logging.Console | Logging |
| AgentSmith.Tests | xunit | Test Framework |
| AgentSmith.Tests | xunit.runner.visualstudio | Test Runner |
| AgentSmith.Tests | Microsoft.NET.Test.Sdk | Test Infrastructure |
| AgentSmith.Tests | Moq | Mocking |
| AgentSmith.Tests | FluentAssertions | Readable Assertions |

---

## Definition of Done (Phase 1)
- [ ] Solution compiles without errors
- [ ] All Domain Entities with properties defined
- [ ] All Value Objects defined as records
- [ ] All Interfaces defined in Contracts
- [ ] Config Loader reads YAML and returns typed config
- [ ] Example `agentsmith.yml` present
- [ ] `coding-principles.md` present
- [ ] At least 1 unit test (Config Loader)
- [ ] All files adhere to Coding Principles (20/120 rule)
