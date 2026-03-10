# Phase 37: Strategy Pattern Pipeline Abstraction

## Context

Agent Smith is currently hardwired for coding tasks. Source = Git, Bootstrap = detect .csproj/package.json,
Test = `dotnet test`/`npm test`, Output = Pull Request. To support new use cases (claude-to-claude
discussions, data pipelines, content generation), the pipeline must become type-driven.

The goal: a ticket that says "discuss philosophy in three rounds" uses the **same pipeline engine**
as a ticket that says "fix this bug", but with different strategies for source, bootstrap, execution,
testing, and output.

## Principle

**Tell, don't ask.** No `if (type == "coding")` anywhere. The pipeline type resolves the correct
strategy implementations via DI. New types = new strategy classes registered, zero existing code changed.

**Small classes.** Nothing over 120 lines. The current PipelineExecutor (212), CommandContextFactory (223),
BootstrapProjectHandler (317), WriteRunResultHandler (246) must be decomposed.

## Architecture

### Strategy Interfaces (in Contracts)

```
ISourceProvider          — acquires the workspace
IGitSourceProvider       : ISourceProvider — Git clone/branch/commit/push/PR
ISftpSourceProvider      : ISourceProvider — SFTP file download
IBlobSourceProvider      : ISourceProvider — Azure Blob / S3
INoOpSourceProvider      : ISourceProvider — empty workspace (pure discussion)

IContextBootstrapper     — understands the workspace content
ICodingBootstrapper      : IContextBootstrapper — detects project type, frameworks, test commands
IDiscussionBootstrapper  : IContextBootstrapper — loads persona definitions, discussion rules

IExecutionStrategy       — the "do the work" step
ICodingExecutionStrategy : IExecutionStrategy — agentic loop with bash/file/build tools
IDiscussionStrategy      : IExecutionStrategy — agentic loop with read/write-only tools (no bash)

ITestStrategy            — validates the output
ICodingTestStrategy      : ITestStrategy — dotnet test / npm test / pytest
IContentValidator        : ITestStrategy — checks for quality, profanity, completeness

IOutputStrategy          — delivers the result
IPullRequestOutput       : IOutputStrategy — Git commit + PR + close ticket
ISharePointOutput        : IOutputStrategy — write to SharePoint list
IMarkdownOutput          : IOutputStrategy — write markdown to repo/blob
```

### Type Resolution

```yaml
# In .agentsmith/context.yaml or skill.yaml
agent:
  type: coding          # or: discussion, data-pipeline, content, ...
```

Or from the ticket/command itself:
```
/discuss "Who are we and why?"         → type: discussion
/fix-bug "Login page broken"           → type: coding
/analyze "Q4 sales data in blob://..." → type: data-pipeline
```

The Dispatcher resolves the type early and passes it as a pipeline parameter.
DI registration uses keyed services (.NET 8) or a strategy factory:

```csharp
services.AddKeyedTransient<ISourceProvider, GitSourceProvider>("coding");
services.AddKeyedTransient<ISourceProvider, NoOpSourceProvider>("discussion");
```

### Pipeline Steps (unchanged names, strategy-resolved behavior)

```
FetchTicket       — generic (ITicketProvider, already abstracted)
AcquireSource     — was: CheckoutSource → now: ISourceProvider.AcquireAsync()
Bootstrap         — was: BootstrapProject → now: IContextBootstrapper.BootstrapAsync()
LoadCodeMap       — generic (read file from workspace, skip if absent)
LoadDomainRules   — generic (read file from workspace, skip if absent)
LoadContext        — generic (read file from workspace, skip if absent)
Analyze           — generic (LLM call, prompt varies by type)
Triage            — generic (LLM decides participants, personas instead of dev roles)
GeneratePlan      — generic (LLM call, prompt varies by type)
Approval          — generic (human approval gate)
Execute           — was: AgenticExecute → now: IExecutionStrategy.ExecuteAsync()
Validate          — was: Test → now: ITestStrategy.ValidateAsync()
GenerateTests     — coding-only (skip via NoOp strategy for discussion)
GenerateDocs      — coding-only (skip via NoOp strategy for discussion)
WriteResult       — generic (write run result to workspace)
DeliverOutput     — was: CommitAndPR → now: IOutputStrategy.DeliverAsync()
```

SkillRound, SwitchSkill, ConvergenceCheck remain unchanged — they are already type-agnostic.

### Persona Definitions (for Discussion type)

```yaml
# config/personas/philosopher.yaml
name: philosopher
display_name: "The Philosopher"
emoji: "..."
description: "Explores fundamental questions, challenges assumptions, seeks deeper meaning"

rules: |
  You are a philosopher participating in a group discussion.
  Think deeply. Question premises. Seek truth over agreement.
  Write your thoughts to discussion/{your-name}-round-{N}.md.
  Read other participants' files before each round.

convergence_criteria:
  - "Core question has been addressed from multiple angles"
  - "Participants have engaged with each other's arguments"
```

Same YAML schema as coding skills — loaded by the same `SkillLoader`.

### Trigger Sources (beyond tickets)

The Dispatcher already has intent routing. New intents:

```
/discuss "topic"              → Discussion pipeline, personas from triage
/discuss-with poet,dreamer    → Discussion pipeline, explicit personas
/gather 5 3                   → 5 personas, 3 rounds, free-form
```

Future triggers (not in this phase, but the architecture supports them):
- Blob file watcher (new file in container → trigger pipeline)
- Scheduled cron (daily philosophical reflection)
- Webhook (external system POST → trigger pipeline)

## Decomposition Plan

### Step 1: Extract Strategy Interfaces
- Define `ISourceProvider`, `IContextBootstrapper`, `IExecutionStrategy`, `ITestStrategy`, `IOutputStrategy` in Contracts
- Each interface: single method + type discriminator property

### Step 2: Refactor Existing Handlers into Strategies
- `CheckoutSourceHandler` → `GitSourceProvider : ISourceProvider`
- `BootstrapProjectHandler` (317 lines!) → split into `CodingBootstrapper` (<120 lines) + helpers
- `TestHandler` → `CodingTestStrategy : ITestStrategy`
- `CommitAndPRHandler` → `PullRequestOutputStrategy : IOutputStrategy`
- `AgenticExecuteHandler` → `CodingExecutionStrategy : IExecutionStrategy`

### Step 3: Eliminate Switch Statements
- `PipelineExecutor.ExecuteCommandAsync()` switch (20 cases) → generic dispatch via `ICommandHandler<T>` resolution
- `CommandContextFactory` switch (21 cases) → strategy-based context builders, or collapse into fewer generic contexts

### Step 4: Add Discussion Type
- `NoOpSourceProvider` — empty workspace
- `DiscussionBootstrapper` — loads persona configs
- `DiscussionExecutionStrategy` — agentic loop, read/write only, no bash
- `ContentValidator` — basic quality check
- `MarkdownOutputStrategy` — PR with discussion files, or alternative output

### Step 5: Wire DI with Type Resolution
- Pipeline type flows from Dispatcher → Host → DI scope
- Keyed services or factory pattern resolves correct strategies

### Step 6: New Slack Commands
- `/discuss` intent in Dispatcher
- Persona selection (triage or explicit)
- Round control (default 3, configurable)

## Files Affected

### New (Contracts — Interfaces)
- `ISourceProvider.cs`, `IContextBootstrapper.cs`, `IExecutionStrategy.cs`, `ITestStrategy.cs`, `IOutputStrategy.cs`

### Refactored (Handlers → Strategies)
- `CheckoutSourceHandler.cs` → `GitSourceProvider.cs`
- `BootstrapProjectHandler.cs` (317 lines) → `CodingBootstrapper.cs` + extracted helpers
- `TestHandler.cs` (137 lines) → `CodingTestStrategy.cs`
- `CommitAndPRHandler.cs` → `PullRequestOutputStrategy.cs`
- `AgenticExecuteHandler.cs` → `CodingExecutionStrategy.cs`

### Simplified (Switch elimination)
- `PipelineExecutor.cs` (212 lines → <120)
- `CommandContextFactory.cs` (223 lines → <120)

### New (Discussion type)
- `NoOpSourceProvider.cs`
- `DiscussionBootstrapper.cs`
- `DiscussionExecutionStrategy.cs`
- `ContentValidator.cs`
- `MarkdownOutputStrategy.cs`
- `config/personas/*.yaml` (philosopher, dreamer, poet, etc.)

### Modified
- `ServiceCollectionExtensions.cs` — keyed service registration
- `PipelinePresets.cs` — new Discussion preset
- `CommandNames.cs` — renamed steps (CheckoutSource → AcquireSource, Test → Validate, etc.)
- Dispatcher intent handlers — new `/discuss` command

## Risks

- Large refactoring surface — existing 427 tests must keep passing
- Strategy resolution adds indirection — keep it simple (factory, not framework)
- Renaming pipeline steps is breaking for existing `.agentsmith/context.yaml` in target repos
  → Migration: accept both old and new names during transition

## Out of Scope (future phases)

- SFTP/Blob source providers (interface only, no implementation)
- SharePoint output strategy (interface only)
- Scheduled triggers / blob watchers
- Web UI for discussion monitoring
- Persistent discussion memory across sessions
