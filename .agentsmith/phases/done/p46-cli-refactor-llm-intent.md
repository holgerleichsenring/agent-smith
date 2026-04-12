# Phase 46: CLI Refactor, LLM Intent Parsing, Ticketless Cleanup

## Goal

Replace free-text regex parsing with explicit CLI flags and LLM-based intent
parsing for Slack. Split oversized files. Make all verbs dry-runnable. Eliminate
the "ticketless" concept — every pipeline type is first-class.

---

## The Problem

1. **`Program.cs` is 456 lines.** Limit is 120. Three command handlers, helper
   methods, banner, service provider setup — all in one file.

2. **`ExecutePipelineUseCase` uses regex to guess intent.** `TicketlessPattern`
   is a growing regex (`security-scan|legal-analysis|api-scan|...`) that breaks
   every time a new pipeline type is added. The "ticketless" vs "ticket" split
   is a leaky abstraction — the pipeline type determines what's needed, not
   whether a ticket exists.

3. **`run` subcommand parses free text.** `"fix #42 in my-api"` goes through
   `RegexIntentParser` which extracts ticket ID and project name via pattern
   matching. Fragile, not self-documenting, impossible to extend.

4. **Slack intent parsing is regex-based.** `ChatIntentParser` uses regex too.
   Works for known patterns, fails for anything new.

5. **Several Dispatcher files exceed 120 lines.**

---

## Architecture Decisions (final)

- **CLI: explicit flags only.** No free-text parsing in the CLI.
- **Slack: LLM intent parsing via Haiku.** Free text is natural for chat — but
  the parser is an LLM, not a regex. Returns structured JSON.
- **No "ticketless" concept.** Each pipeline type declares what it needs. The
  use case routes by pipeline type, not by regex pattern matching.
- **Program.cs → command registrations only.** Handler logic moves to dedicated
  classes.

---

## CLI Refactor

### Before
```bash
agent-smith run "fix #42 in my-api"
agent-smith run "fix #42 in my-api" --pipeline add-feature
```

### After: One verb per pipeline
```bash
agent-smith fix --ticket 42 --project my-api
agent-smith feature --ticket 42 --project my-api
agent-smith init --project my-api
agent-smith mad --ticket 42 --project my-api
agent-smith legal --source ./contract.pdf
agent-smith security-scan --repo . --project my-api       # unchanged
agent-smith api-scan --swagger ./spec.json --target https://...  # unchanged
agent-smith server --port 8081                             # unchanged
```

Pattern: `{program} {verb} {args}`. Each verb is a pipeline. No `run`, no
`--pipeline`. Each verb knows exactly which flags it needs.

All verbs support `--dry-run`, `--config`, `--verbose`.

Breaking change: `agent-smith run` is removed. `run` is not a verb.

### Backward compatibility
Keep `run` as hidden deprecated command for one release. If used, log a
deprecation warning and delegate to the appropriate verb. Remove in next major.

---

## Program.cs Split

Current: 456 lines, everything in one file.

Split into:

```
src/AgentSmith.Cli/
  Program.cs                    ← ~30 lines: root command, add verbs, return
  Commands/
    FixCommand.cs               ← fix verb (fix-bug pipeline)
    FeatureCommand.cs           ← feature verb (add-feature pipeline)
    InitCommand.cs              ← init verb (init-project pipeline)
    MadCommand.cs               ← mad verb (mad-discussion pipeline)
    LegalCommand.cs             ← legal verb (legal-analysis pipeline)
    SecurityScanCommand.cs      ← security-scan verb
    ApiScanCommand.cs           ← api-scan verb
    ServerCommand.cs            ← server verb
    SharedOptions.cs            ← --config, --verbose, --dry-run, --project, --headless
  Banner.cs                     ← PrintBanner()
  ServiceProviderFactory.cs     ← BuildServiceProvider()
  DryRunPrinter.cs              ← unified dry-run output
```

Each `*Command` class: static `Create()` returns a configured `Command`.
Under 120 lines each. Shared options extracted to avoid duplication.

---

## ExecutePipelineUseCase Refactor

### Before
```csharp
var initMatch = InitPattern.Match(userInput);
var ticketlessMatch = TicketlessPattern.Match(userInput);
// falls through to ticket-based execution
```

### After
```csharp
public Task<CommandResult> ExecuteAsync(PipelineRequest request, ...)

public sealed record PipelineRequest(
    string ProjectName,
    string PipelineName,
    TicketId? TicketId,               // null for ticketless pipelines
    Dictionary<string, object>? Context);  // swagger path, target URL, etc.
```

No regex. No pattern matching. The CLI builds the `PipelineRequest` explicitly.
Slack's LLM intent parser builds it too. Same record, same code path.

The `ExecuteTicketAsync` / `ExecuteTicketlessAsync` / `ExecuteInitAsync` split
disappears. One method: `ExecuteAsync(PipelineRequest)`.

---

## LLM Intent Parser for Slack

Replace `RegexIntentParser` and `ChatIntentParser` with `LlmIntentParser`:

```csharp
public sealed class LlmIntentParser(ILlmClientFactory llmClientFactory) : IIntentParser
{
    public async Task<PipelineRequest> ParseAsync(string userInput, ...)
    {
        // System prompt: list of available pipelines, projects, parameters
        // User input: free text
        // Response: structured JSON → PipelineRequest
    }
}
```

Model: Haiku (fast, cheap — ~0.001$ per intent).

The system prompt includes:
- Available pipeline names and what each needs
- Available project names from config
- Expected JSON response format

This replaces both `RegexIntentParser` and `ChatIntentParser`. The Haiku call
is the only parser. No regex fallback.

---

## Dry-Run Consolidation

Current: `RunDryMode`, `RunTicketlessDryMode`, `PrintDryRun` — three methods.

After: One method that takes a `PipelineRequest` and prints the resolved pipeline.
Lives in its own class, not in Program.cs.

```csharp
public static class DryRunPrinter
{
    public static void Print(PipelineRequest request, IReadOnlyList<string> commands)
    {
        Console.WriteLine("Dry run - would execute:");
        Console.WriteLine($"  Project:  {request.ProjectName}");
        Console.WriteLine($"  Pipeline: {request.PipelineName}");
        if (request.TicketId is not null)
            Console.WriteLine($"  Ticket:   #{request.TicketId}");
        // print context entries (swagger, target, repo, etc.)
        // print commands
    }
}
```

---

## Files Over 120 Lines (candidates for split)

| File | Lines | Action |
|------|-------|--------|
| Program.cs | 456 | Split into 6 files (see above) |
| WebApplicationExtensions.cs | 410 | Split middleware registration by area |
| MessageBusListener.cs | 309 | Extract message handlers |
| SlackAdapter.cs | 307 | Extract API call helpers |
| SlackModalBuilder.cs | 289 | Extract block builders |
| SlackModalSubmissionHandler.cs | 273 | Extract per-command handlers |
| ContextGenerator.cs | 230 | Extract prompt builders |
| JiraTicketProvider.cs | 223 | Extract ADF parser |
| DockerJobSpawner.cs | 223 | Extract container config builder |
| ExecutePipelineUseCase.cs | 189 | Collapses to ~80 after PipelineRequest refactor |

Priority: Program.cs and ExecutePipelineUseCase (blocking for this phase).
Rest: follow-up or parallel work.

---

## Files to Create

- `src/AgentSmith.Cli/Commands/RunCommandSetup.cs`
- `src/AgentSmith.Cli/Commands/SecurityScanCommandSetup.cs`
- `src/AgentSmith.Cli/Commands/ApiScanCommandSetup.cs`
- `src/AgentSmith.Cli/Commands/ServerCommandSetup.cs`
- `src/AgentSmith.Cli/Banner.cs`
- `src/AgentSmith.Cli/ServiceProviderFactory.cs`
- `src/AgentSmith.Cli/DryRunPrinter.cs`
- `src/AgentSmith.Application/Models/PipelineRequest.cs`
- `src/AgentSmith.Application/Services/LlmIntentParser.cs`

## Files to Modify

- `src/AgentSmith.Cli/Program.cs` — shrink to ~40 lines
- `src/AgentSmith.Application/Services/ExecutePipelineUseCase.cs` — PipelineRequest-based
- `src/AgentSmith.Server/Services/Handlers/SlackMessageDispatcher.cs` — use LlmIntentParser
- `src/AgentSmith.Contracts/Services/IIntentParser.cs` — return PipelineRequest

## Files to Delete

- `src/AgentSmith.Application/Services/RegexIntentParser.cs` (replaced)
- `src/AgentSmith.Application/Services/ChatIntentParser.cs` (replaced)

---

## Definition of Done

- [ ] `Program.cs` under 30 lines
- [ ] Each command file under 120 lines
- [ ] `ExecutePipelineUseCase` accepts `PipelineRequest`, no regex
- [ ] `agent-smith fix --ticket 42 --project my-api` works
- [ ] `agent-smith feature --ticket 42 --project my-api` works
- [ ] `agent-smith init --project my-api` works
- [ ] `agent-smith mad --ticket 42 --project my-api` works
- [ ] `agent-smith legal --source ./contract.pdf` works
- [ ] `agent-smith run` shows deprecation warning, delegates to correct verb
- [ ] All verbs support `--dry-run` via `DryRunPrinter`
- [ ] `LlmIntentParser` returns structured JSON via Haiku
- [ ] Slack free text → Haiku → `PipelineRequest` → pipeline execution
- [ ] No file over 120 lines in Host or Application layers
- [ ] All existing tests green + new tests for PipelineRequest routing
