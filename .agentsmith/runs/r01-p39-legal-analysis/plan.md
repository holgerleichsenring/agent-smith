# Run r01 — Phase 39: Legal Analysis Pipeline

## Scope

Implement the legal analysis pipeline end-to-end. OSS gets minimal additions
(command names, preset, context keys). All business logic lives in agent-smith-pro.

## Steps

### Step 1: OSS Contracts — New command names and context keys
- Add `AcquireSource`, `BootstrapDocument`, `DeliverOutput` to `CommandNames.cs` + labels
- Add `DocumentMarkdown`, `ContractType`, `SourceFilePath` to `ContextKeys.cs`
- Add `LegalAnalysis` preset to `PipelinePresets.cs`

### Step 2: Pro — Context records and builders
New commands need context records + builders (same pattern as OSS Application):
- `AcquireSourceContext` + `AcquireSourceContextBuilder`
- `BootstrapDocumentContext` + `BootstrapDocumentContextBuilder`
- `DeliverOutputContext` + `DeliverOutputContextBuilder`

### Step 3: Pro — AcquireSourceHandler
- Reads source file path from `ContextKeys.SourceFilePath` (set by InboxPollingService)
- Creates workspace directory, copies file there
- Creates `Repository` entity (LocalPath = workspace, dummy branch/remote)
- Sets `ContextKeys.Repository` in pipeline

### Step 4: Pro — BootstrapDocumentHandler
- Runs `markitdown {inputPath}` subprocess, captures stdout as Markdown
- Detects contract type via Haiku call on first 500 tokens
- Writes `contract.md` to workspace
- Sets `ContextKeys.DocumentMarkdown` and `ContextKeys.ContractType`
- Sets `ContextKeys.AvailableRoles` by loading skills from `config/skills/legal/`

### Step 5: Pro — DeliverOutputHandler
- Reads compiled discussion from `ContextKeys.CodeChanges` (reused from CompileDiscussion)
- Writes analysis to `./outbox/{timestamp}-{filename}-analysis.md`
- Moves source from `./processing/` to `./archive/`
- Deletes original from `./inbox/`
- Publishes `AnalysisCompletedEvent` via MessageBus (if available)

### Step 6: Pro — InboxPollingService
- `BackgroundService` with `PeriodicTimer` (5s interval)
- Polls `./inbox/` for new files
- Copies to `./processing/` (overwriting)
- Enqueues pipeline job with `SourceFilePath` in context
- Startup scan of `./processing/` for orphaned files

### Step 7: Pro — DI wiring in ServiceCollectionExtensions
- Register all handlers as `ICommandHandler<T>`
- Register context builders as `KeyedContextBuilder`
- Register `InboxPollingService` as `IHostedService`

### Step 8: Unit tests
- AcquireSourceHandler: workspace creation, file copy
- BootstrapDocumentHandler: subprocess mock, contract type detection
- DeliverOutputHandler: file output, archive move
- InboxPollingService: file detection, copy, orphan recovery

## Design Decisions
- No strategy interfaces needed yet — handlers use concrete implementations directly
- `Repository` entity reused as workspace reference (dummy branch/remote for non-Git)
- Pro registers additional `KeyedContextBuilder` entries → OSS `CommandContextFactory`
  picks them up automatically via `IEnumerable<KeyedContextBuilder>` injection
- MarkItDown called via `Process.Start()` — simple, no Python interop needed
- Existing Triage/SkillRound/ConvergenceCheck/CompileDiscussion handlers reused as-is
