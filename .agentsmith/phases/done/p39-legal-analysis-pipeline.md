# Phase 39: Legal Analysis Pipeline

## Goal

Enable Agent Smith to analyze legal documents (contracts, NDAs, etc.) using a
skill-based discussion pipeline. Input: PDF file (via inbox polling or Slack upload).
Output: structured Markdown report in `./outbox/`.

This phase reuses the MAD discussion pipeline machinery (Phase 38) with a new skill
category (`legal`), a new source provider (`LocalFolderSourceProvider`), and a new
output strategy (`LocalFileOutputStrategy`). No SharePoint, no Azure Blob, no ticket
system required for the first run.

**Commercial feature:** All new code lives in the `agent-smith-pro` repository.
The OSS repo only receives the strategy interfaces (prerequisite from p37) that were
designed but not yet implemented.

---

## Prerequisites

### Strategy interfaces in OSS Contracts

p37 defined these interfaces conceptually but they were never created. They must be
added to `AgentSmith.Contracts` before this phase can begin:

```
IContextBootstrapper    — understands the workspace content
IExecutionStrategy      — the "do the work" step
ITestStrategy           — validates the output
IOutputStrategy         — delivers the result
```

`ISourceProvider` already exists but is Git-specific. It needs either:
- A more generic base interface, or
- A parallel `IDocumentSourceProvider` for non-Git sources

These are generic abstractions — they belong in OSS Contracts.

### New pipeline command names in OSS

- `CommandNames.AcquireSource` — `"AcquireSourceCommand"`
- `CommandNames.BootstrapDocument` — `"BootstrapDocumentCommand"`
- `CommandNames.DeliverOutput` — `"DeliverOutputCommand"`

### New pipeline preset in OSS

```csharp
public static readonly IReadOnlyList<string> LegalAnalysis =
[
    CommandNames.AcquireSource,
    CommandNames.BootstrapDocument,
    CommandNames.LoadDomainRules,
    CommandNames.Triage,
    CommandNames.ConvergenceCheck,
    CommandNames.CompileDiscussion,
    CommandNames.DeliverOutput,
];
```

TriageCommand dynamically inserts SkillRound commands between Triage and ConvergenceCheck
(same mechanism as MAD discussion).

---

## Architecture

### How it fits into p37 Strategy Pattern

All strategy interfaces will exist in OSS Contracts. This phase adds implementations
in the Pro repo only:

```
ISourceProvider       > LocalFolderSourceProvider  (reads from ./processing/)
IContextBootstrapper  > DocumentBootstrapper        (MarkItDown PDF > Markdown)
IExecutionStrategy    > DiscussionExecutionStrategy (reused from p38, read/write only)
ITestStrategy         > NoOpTestStrategy            (no validation in v1)
IOutputStrategy       > LocalFileOutputStrategy     (writes to ./outbox/)
```

Pipeline type: `legal`
Skills path: `config/skills/legal/` (in Pro repo)

---

## Trigger: How a new document is detected

### Option A: Inbox Polling (primary)

```
./inbox/     > new PDF dropped here (manually, or by Slack adapter)
./outbox/    > structured Markdown report written here
./archive/   > processed PDFs moved here after completion
```

`InboxPollingService` (IHostedService) polls `./inbox/` every 5-10 seconds via
`Directory.GetFiles()`. No FileSystemWatcher — polling is more reliable on Docker
bind mounts and works consistently across platforms.

On new file detected:
1. Copy PDF to `./processing/{filename}` (overwriting, idempotent)
2. Enqueue `LegalAnalysisCommand` into Dispatcher queue
3. After pipeline completes successfully:
   - Delete original from `./inbox/`
   - Move from `./processing/` to `./archive/{timestamp}-{filename}`
4. On startup: scan `./processing/` for orphaned files from crashed runs, re-enqueue

Copy-then-delete (not move) ensures crash safety: if the app dies between copy and
enqueue, the file remains in `./inbox/` and will be picked up on next poll cycle.

### Option B: Slack upload (additive, same inbox)

Slack `file_shared` event > SlackAdapter downloads PDF > drops into `./inbox/`.
Polling fires. Pipeline runs. Result posted back to original Slack channel/thread.

The Slack channel/thread ID is passed as metadata alongside the file
(e.g., `{filename}.meta.json` with `{ "replyTo": "C123/T456" }`).

Both options share the same pipeline. Source of the PDF is irrelevant after inbox.

### Output delivery (event-based, no direct coupling)

`LocalFileOutputStrategy` writes the analysis to `./outbox/` and publishes an
`AnalysisCompletedEvent` on the MessageBus with metadata (file path, original
filename, optional replyTo info from `.meta.json`).

Platform adapters (Slack, Teams) subscribe to this event. If `replyTo` metadata
is present, the adapter posts the result back to the originating channel/thread.
No direct dependency between OutputStrategy and any platform adapter.

---

## Document Preprocessing: MarkItDown

Before the LLM sees the document, MarkItDown converts the PDF to clean Markdown.

**Why not send PDF directly to Claude?**
- Token efficiency: Markdown ~60% fewer tokens than raw PDF base64
- Searchable: clause references in the discussion log are human-readable
- Consistent: same preprocessing for PDF, DOCX, and future formats

**Integration point:** `DocumentBootstrapper.BootstrapAsync()`

```csharp
// DocumentBootstrapper.cs (in AgentSmith.Pro)
// 1. Run MarkItDown via subprocess (Python CLI, installed in Docker image)
// 2. Write output to workspace as contract.md
// 3. Store path in PipelineContext under ContextKeys.DocumentMarkdown
// 4. Store detected contract type (NDA / Werkvertrag / etc.) under ContextKeys.ContractType
```

Contract type detection: single cheap Haiku call on the first 500 tokens of the Markdown.
Result feeds into Triage to select the right skill subset.

---

## Pipeline Preset: `legal-analysis`

```
AcquireSource         > LocalFolderSourceProvider reads PDF from ./processing/
Bootstrap             > DocumentBootstrapper: MarkItDown > contract.md + detect type
LoadDomainRules       > loads config/skills/legal/legal-principles.md
Triage                > selects legal skills based on contract type
[SkillRounds]         > inserted dynamically by Triage
ConvergenceCheck      > consensus reached or max rounds hit
CompileDiscussion     > formats findings into structured report (reused from p38)
DeliverOutput         > LocalFileOutputStrategy writes to ./outbox/ (+ event for Slack/Teams)
```

No `CheckoutSource` (no Git). No `GeneratePlan`. No `AgenticExecute`. No `Test`. No `CommitAndPR`.

---

## Legal Skills

All files in `config/skills/legal/` (Pro repo). Same YAML schema as coding and MAD skills.

### Triage selects based on contract type

| Contract type       | Always                           | Situational                                              |
|---------------------|----------------------------------|----------------------------------------------------------|
| NDA                 | contract-analyst, risk-assessor  | liability-analyst                                        |
| Werkvertrag         | contract-analyst, risk-assessor  | compliance-checker, liability-analyst, clause-negotiator  |
| Dienstleistung      | contract-analyst, risk-assessor  | compliance-checker, liability-analyst                     |
| SaaS-AGB            | contract-analyst, risk-assessor  | compliance-checker, clause-negotiator                     |
| Unknown             | contract-analyst, risk-assessor  | (Triage decides)                                         |

### Skill files

- `contract-analyst.yaml` — reads contract systematically, identifies all clauses
- `risk-assessor.yaml` — assigns risk levels (HIGH/MEDIUM/LOW) per clause
- `liability-analyst.yaml` — deep-dives into liability caps, exclusions, indemnification
- `compliance-checker.yaml` — DSGVO compliance and AGB-Recht validity
- `clause-negotiator.yaml` — proposes alternative formulations for problematic clauses

### Domain rules

- `legal-principles.md` — scope, language, perspective, tone, boundaries

---

## Project Config Example

```yaml
# config/agentsmith.yml > legal-nda project entry (in Pro deployment config)

projects:
  legal-nda:
    source:
      type: LocalFolder
      path: ./inbox
    tickets:
      type: None
    agent:
      type: Claude
      model: claude-sonnet-4-20250514
    pipeline: legal-analysis
    skills_path: config/skills/legal

pipelines:
  legal-analysis:
    commands:
      - AcquireSourceCommand
      - BootstrapDocumentCommand
      - LoadDomainRulesCommand
      - TriageCommand
      - ConvergenceCheckCommand
      - CompileDiscussionCommand
      - DeliverOutputCommand
```

---

## Repo Separation: OSS vs Pro

### Goes into OSS (agent-smith)

| What                      | Where                                            |
|---------------------------|--------------------------------------------------|
| Strategy interfaces       | `src/AgentSmith.Contracts/Services/`             |
| New CommandNames          | `src/AgentSmith.Contracts/Commands/CommandNames.cs` |
| LegalAnalysis preset      | `src/AgentSmith.Contracts/Commands/PipelinePresets.cs` |
| AnalysisCompletedEvent    | `src/AgentSmith.Contracts/Events/`               |

### Goes into Pro (agent-smith-pro)

| What                      | Where                                                |
|---------------------------|------------------------------------------------------|
| LocalFolderSourceProvider | `src/AgentSmith.Pro/Providers/Source/`               |
| DocumentBootstrapper      | `src/AgentSmith.Pro/Services/DocumentBootstrapper.cs` |
| LocalFileOutputStrategy   | `src/AgentSmith.Pro/Providers/Output/`               |
| NoOpTestStrategy          | `src/AgentSmith.Pro/Services/NoOpTestStrategy.cs`    |
| InboxPollingService       | `src/AgentSmith.Pro/Triggers/InboxPollingService.cs` |
| Legal skill YAMLs         | `config/skills/legal/`                               |
| Legal principles          | `config/skills/legal/legal-principles.md`            |
| ServiceCollectionExtensions | `src/AgentSmith.Pro/ServiceCollectionExtensions.cs` |

### Pro references OSS via relative project path

```xml
<ProjectReference Include="../../agent-smith/src/AgentSmith.Contracts/AgentSmith.Contracts.csproj" />
```

---

## Docker: MarkItDown Installation

```dockerfile
# In Pro Dockerfile > add alongside existing dependencies
RUN pip install markitdown[all]
```

MarkItDown is MIT-licensed, maintained by Microsoft. The `[all]` extra includes
PDF support via pdfminer, DOCX via mammoth, and OCR via optional tesseract.

---

## Tests

Unit tests for all new components in Pro repo:

- `LocalFolderSourceProvider`: reads from processing dir, creates workspace
- `DocumentBootstrapper`: subprocess call, contract type detection
- `LocalFileOutputStrategy`: writes to outbox, moves to archive, publishes event
- `InboxPollingService`: detects new files, copies to processing, orphan recovery
- `NoOpTestStrategy`: returns success

Integration test: drop PDF in inbox, verify report appears in outbox.

---

## Estimation

~350 lines new code in Pro:
- `LocalFolderSourceProvider`: ~60 lines
- `DocumentBootstrapper`: ~80 lines
- `LocalFileOutputStrategy`: ~70 lines
- `InboxPollingService`: ~50 lines
- `NoOpTestStrategy`: ~15 lines
- `ServiceCollectionExtensions`: ~25 lines
- Tests: ~150 lines

~50 lines in OSS (interfaces, CommandNames, preset, event)

5 YAML skill files + 1 principles file (non-code, domain knowledge)
