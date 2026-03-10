# Phase 38: MAD Discussion Pipeline

## Goal
Enable Agent Smith to run structured multi-agent discussions (MAD — Multiple Agent Debate)
where different Claude personas debate a topic, producing a markdown discussion document as a PR.

## Context
The existing skill system (Phase 34) already has 90% of the machinery:
- `SkillRoundHandler` — pure text, no tools, appends to DiscussionLog
- `ConvergenceCheckHandler` — manages rounds, OBJECTION logic, consolidation
- `TriageHandler` — selects roles dynamically

What's missing: the system assumes code pipelines (CheckoutSource, AnalyzeCode, AgenticExecute).
A discussion pipeline needs to skip code steps, load discussion-specific skills,
and produce a markdown document instead of code changes.

## Architecture Decisions

### Skill loading — `skills_path` on ProjectConfig
The hardcoded `"config/skills"` in `MetaFileBootstrapper` prevents loading from subdirectories.
Add `SkillsPath` to `ProjectConfig` (default: `config/skills`). MetaFileBootstrapper uses it.
MAD projects set `skills_path: config/skills/mad`.

### No new commands for turn management
The Silencer's trigger is pure prompt logic — if conditions aren't met, it responds `[SILENCE]`.
SkillRoundHandler already handles this: the response is appended to the log regardless.
No special command needed.

### CompileDiscussionCommand — new
After ConvergenceCheck, this command takes the DiscussionLog and writes it as a formatted
markdown file (`discussion-{ticket-id}.md`) to the repo. This replaces the code-execution
steps (AnalyzeCode, GeneratePlan, AgenticExecute, Test) in the discussion pipeline.

### Pipeline preset — `mad-discussion`
```
FetchTicket → CheckoutSource → BootstrapProject → LoadContext →
Triage → [SkillRounds inserted dynamically] → ConvergenceCheck →
CompileDiscussion → CommitAndPR
```
No AnalyzeCode, no GeneratePlan, no AgenticExecute, no Test, no WriteRunResult.

### ConvergenceCheck consolidation — reuse as-is
The existing consolidation LLM call works for discussion summaries too.
The `ConsolidatedPlan` becomes the executive summary at the top of the discussion document.

## Tasks

### 1. Add `SkillsPath` to ProjectConfig
- Add `public string SkillsPath { get; set; } = "config/skills";` to `ProjectConfig`
- Update `MetaFileBootstrapper.TryLoadSkillRoles` to use it (passed from BootstrapProjectHandler)
- Update `BootstrapProjectHandler` to pass skills path from project config

### 2. CompileDiscussionHandler
- New command: `CompileDiscussionCommand`
- New handler: `CompileDiscussionHandler`
  - Reads `DiscussionLog` and `ConsolidatedPlan` from pipeline
  - Formats as markdown: executive summary + full transcript with personas
  - Writes to `{repo}/discussion-{ticket-id}.md`
  - Stages the file for commit
- New context: `CompileDiscussionContext`
- New builder: `CompileDiscussionContextBuilder`
- Register in `CommandNames`, `ServiceCollectionExtensions`, context builder registration

### 3. MAD Discussion pipeline preset
- Add `MadDiscussion` to `PipelinePresets`
- Register as `"mad-discussion"` in the lookup dictionary

### 4. Project config for MAD in agentsmith.yml
- Add a `mad-discussion` project entry in `config/agentsmith.yml`
- Points to the same repo (agent-smith)
- Sets `pipeline: mad-discussion`
- Sets `skills_path: config/skills/mad`

### 5. Tests
- `CompileDiscussionHandlerTests` — formats log correctly, writes file
- `PipelinePresetsTests` — resolves `mad-discussion`
- `MetaFileBootstrapperTests` — respects custom skills_path
- Verify existing 426 tests still pass

## Files to create
- `src/AgentSmith.Application/Services/Handlers/CompileDiscussionHandler.cs`
- `src/AgentSmith.Application/Services/Builders/DiscussionContextBuilders.cs`

## Files to modify
- `src/AgentSmith.Contracts/Models/Configuration/ProjectConfig.cs` — add SkillsPath
- `src/AgentSmith.Contracts/Commands/CommandNames.cs` — add CompileDiscussion
- `src/AgentSmith.Contracts/Commands/PipelinePresets.cs` — add MadDiscussion
- `src/AgentSmith.Application/Services/Handlers/MetaFileBootstrapper.cs` — use SkillsPath
- `src/AgentSmith.Application/Services/Handlers/BootstrapProjectHandler.cs` — pass SkillsPath
- `src/AgentSmith.Application/Extensions/ServiceCollectionExtensions.cs` — register new builder
- `config/agentsmith.yml` — add mad-discussion project

## Estimation
~200 lines new code, ~30 lines modified. Small, focused phase.
