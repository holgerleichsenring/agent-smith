# Phase 42: Legal Analysis Pipeline — Handlers, Skills, Inbox Polling

## Goal

Implement the full legal analysis pipeline: document acquisition, Markdown
conversion, contract type detection, legal skill roles, output delivery,
and inbox polling for automated document processing.

The pipeline preset `legal-analysis` and command names were defined in Phase 39.
This phase provides the handler implementations, context models, builders,
skills, and the inbox trigger.

---

## Implementation

### Handlers

**`AcquireSourceHandler`** — Copies a document from the source path to a
temporary workspace. Creates a pseudo-Repository for pipeline context.

**`BootstrapDocumentHandler`** — Converts document to Markdown via MarkItDown
CLI, detects contract type via LLM (Scout task), loads legal skill roles,
sets domain rules from `legal-principles.md`.

**`DeliverOutputHandler`** — Writes compiled analysis to `./outbox/`, archives
processed source to `./archive/`, removes original from `./inbox/`.

### Context Models

- `AcquireSourceContext(SourceConfig, PipelineContext)`
- `BootstrapDocumentContext(Repository, AgentConfig, SkillsPath, PipelineContext)`
- `DeliverOutputContext(SourceConfig, Repository, PipelineContext)`

### Context Builders

`AcquireSourceContextBuilder`, `BootstrapDocumentContextBuilder`,
`DeliverOutputContextBuilder` — registered via `KeyedContextBuilder` pattern.

### Inbox Polling

`InboxPollingService` — `BackgroundService` that polls `./inbox/` for new
documents via `PeriodicTimer`. Copies to `./processing/`, enqueues via
`IInboxJobEnqueuer`. Recovers orphaned files on startup. Skips `.meta.json`.

`IInboxJobEnqueuer` — Interface in `AgentSmith.Contracts.Services`, implemented
by host/dispatcher.

`InboxPollingOptions` — Configurable paths (inbox, processing, outbox, archive)
and poll interval.

### Legal Skills (`config/skills/legal/`)

- `contract-analyst.yaml` — Systematic clause-by-clause analysis
- `compliance-checker.yaml` — DSGVO + AGB-Recht (§305-310 BGB)
- `risk-assessor.yaml` — Risk rating per clause (HIGH/MEDIUM/LOW)
- `liability-analyst.yaml` — Liability caps, exclusions, indemnification
- `clause-negotiator.yaml` — Alternative formulations for risky clauses
- `legal-principles.md` — Scope, language (German), boundaries

All skills use German legal terminology. Output is always in German.

### DI Registration

Handlers and builders registered in `AgentSmith.Application.ServiceCollectionExtensions`.

### NuGet Fix

Upgraded all `Microsoft.Extensions` packages from 10.0.3 to 10.0.5
(Dispatcher from 8.0.1) to resolve transitive dependency downgrades.

---

## Files Created

- `config/skills/legal/*.yaml` + `legal-principles.md` (6 files)
- `src/AgentSmith.Application/Models/LegalContexts.cs`
- `src/AgentSmith.Application/Services/Handlers/AcquireSourceHandler.cs`
- `src/AgentSmith.Application/Services/Handlers/BootstrapDocumentHandler.cs`
- `src/AgentSmith.Application/Services/Handlers/DeliverOutputHandler.cs`
- `src/AgentSmith.Application/Services/Builders/LegalContextBuilders.cs`
- `src/AgentSmith.Application/Services/Triggers/InboxPollingService.cs`
- `src/AgentSmith.Contracts/Services/IInboxJobEnqueuer.cs`

## Files Modified

- `src/AgentSmith.Application/Extensions/ServiceCollectionExtensions.cs` — handler + builder registration
- `src/AgentSmith.Application/AgentSmith.Application.csproj` — Hosting.Abstractions reference
- `*.csproj` (6 files) — Microsoft.Extensions 10.0.3→10.0.5

## Tests Created

- `AcquireSourceHandlerTests` (2 tests)
- `DeliverOutputHandlerTests` (2 tests)
- `InboxPollingServiceTests` (3 tests)

---

## Definition of Done

- [x] `AcquireSourceHandler` acquires document to workspace
- [x] `BootstrapDocumentHandler` converts, detects type, loads skills
- [x] `DeliverOutputHandler` writes to outbox, archives source
- [x] `InboxPollingService` polls inbox, enqueues jobs, recovers orphans
- [x] `IInboxJobEnqueuer` interface in Contracts
- [x] Legal skills (5 roles + principles) in `config/skills/legal/`
- [x] DI registration for handlers + builders
- [x] NuGet dependency fix (10.0.3→10.0.5)
- [x] All 473 tests green
