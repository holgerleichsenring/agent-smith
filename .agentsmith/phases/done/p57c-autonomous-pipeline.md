# Phase 57c: Autonomous Pipeline

## Goal

A new pipeline type `autonomous` that runs without a ticket and produces tickets.
Agent Smith observes a project, understands what matters, and decides what should
be done next — without being told. The output is not code, not a PR, but a set
of tickets that reflect Agent Smith's own assessment of what the project needs.

This is not automation. This is agency.

## What Makes It Autonomous

Today: Holger writes a ticket → Agent Smith executes it.
After p57c: Agent Smith writes the tickets.

The decision of what to improve is made by the agent — through multi-skill
analysis and convergence — not by a human. The human decides whether to act
on the tickets, not what the tickets should say.

## Trigger

Not a ticket. Time or explicit CLI call:

```bash
# Manual
agent-smith autonomous --project todo-liste
agent-smith autonomous --project agent-smith

# Scheduled (in agentsmith.yml)
projects:
  todo-liste:
    pipeline: autonomous
    trigger:
      schedule: "0 8 * * 1"   # every Monday 8am
    autonomous:
      max_tickets: 3           # don't flood the backlog
      min_confidence: 7        # only high-confidence findings
      ticket_provider: github  # where to write tickets
```

## Pipeline Steps

```
LoadContext          ← context.yaml, code-map, coding-principles
LoadRuns             ← last N runs: results, decisions, costs, findings
LoadVision           ← project-vision.md (what matters for this project)
Triage               ← which skill roles are relevant for this project?
SkillRounds          ← multi-agent analysis (same mechanism as fix-bug)
ConvergenceCheck     ← agree on top findings
WriteTickets         ← create tickets in ticket provider
WriteRunResult       ← log the autonomous run
```

## The Skill Rounds

The same multi-skill mechanism as fix-bug and security-scan —
but the question is different.

Not: "what is wrong with this code?"
But: "what should be improved in this project?"

Each role brings its own perspective:

**Architect** — reads code-map and architecture decisions.
Asks: is the structure still right? are there patterns that drift from
the original design? are there dependencies that should not exist?

**Developer** — reads coding-principles and recent PRs.
Asks: are there recurring patterns in the code that violate principles?
are there areas with low test coverage that have changed often?
are there technical debt items that keep causing bugs?

**Product Owner** — reads project-vision.md and run history.
Asks: are there features that were planned but never started?
are there tickets that keep getting reopened? does the project
still serve its stated purpose?

**Security** — reads last security scan findings and history.
Asks: are there findings that were accepted but not fixed?
are there patterns that suggest systemic issues?

Triage decides which roles are relevant. A pure backend project may not
need a Product Owner round. A project with no security history skips Security.

## ConvergenceCheck

Same mechanism as today — but consensus is about priority, not findings.

The roles must agree: these are the top 3 things this project needs.
Not a list of everything that could be better. Three things, ranked, with reasoning.

If roles disagree, another round runs. If no consensus after N rounds —
the autonomous run produces a "no consensus" result and Holger is notified.
No tickets are written without convergence.

## WriteTicketsHandler

New handler. Takes the converged findings and writes tickets:

```csharp
public sealed class WriteTicketsHandler(
    ITicketProviderFactory ticketFactory,
    ILogger<WriteTicketsHandler> logger)
    : ICommandHandler<WriteTicketsContext>
```

Each ticket gets:
- Title: the finding in one sentence
- Body: reasoning from convergence, which roles agreed, confidence score
- Label: `agent-smith-autonomous`
- Priority: derived from confidence and role agreement

The `agent-smith-autonomous` label is important — it signals that this ticket
was written by the agent, not a human. Holger can filter, review, and decide
whether to act.

## project-vision.md

New file, created during Bootstrap (p22). Without it, the autonomous pipeline
runs without Product Owner role.

```markdown
# Project Vision

## Who uses this
[one sentence — who are the users?]

## What matters most
[one sentence — what is the primary quality criterion?]

## What is explicitly not wanted
[one or two sentences — what should never be added?]

## Definition of good enough
[one sentence — when is a feature or fix complete?]
```

Written once by Holger during project setup. Never generated — this is
human knowledge that cannot be inferred from code.

Bootstrap (p22) asks:
"Describe this project in four sentences: who uses it, what matters most,
what is explicitly not wanted, and when is something good enough?"

The answer becomes `project-vision.md`.

## What Agent Smith Learns Over Time

Each autonomous run produces a `WriteRunResult`. Over time:

- Tickets written → tickets acted on: did Holger accept the suggestions?
- Which roles consistently find actionable items?
- Which findings were rejected? (feeds back into future runs — don't suggest this again)

This is the feedback loop. The agent gets better at knowing what matters
for each specific project because it sees which of its tickets were used.

## agentsmith.yml Configuration

```yaml
projects:
  todo-liste:
    source:
      type: github
      url: https://github.com/kunde/todo-liste
      auth: token
    tickets:
      type: github
      auth: token
    agent:
      type: Claude
      model: claude-sonnet-4-20250514
    pipeline: autonomous
    trigger:
      schedule: "0 8 * * 1"
    autonomous:
      max_tickets: 3
      min_confidence: 7
      lookback_runs: 10        # how many past runs to analyze
      roles: auto              # triage decides, or explicit: [architect, developer]
```

## Files to Create

- `src/AgentSmith.Contracts/Commands/CommandNames.cs`
  — add `LoadVision`, `WriteTickets`
- `src/AgentSmith.Application/Services/Handlers/LoadVisionHandler.cs`
- `src/AgentSmith.Application/Services/Handlers/WriteTicketsHandler.cs`
- `src/AgentSmith.Application/Models/LoadVisionContext.cs`
- `src/AgentSmith.Application/Models/WriteTicketsContext.cs`
- `src/AgentSmith.Contracts/Commands/PipelinePresets.cs`
  — add `Autonomous` preset
- `config/templates/project-vision.md` — template for Bootstrap
- `config/skills/autonomous/` — skill overrides for autonomous analysis mode

## Files to Modify

- `src/AgentSmith.Application/Services/Handlers/BootstrapProjectHandler.cs`
  — prompt for project-vision.md during setup
- `src/AgentSmith.Cli/Commands/PipelineCommand.cs`
  — add `autonomous` verb
- `config/agentsmith.yml` — add autonomous example project

## Definition of Done

- [ ] `agent-smith autonomous --project X` runs end-to-end
- [ ] `LoadVisionHandler` reads project-vision.md (graceful if missing)
- [ ] `LoadRunsHandler` reads last N runs from `.agentsmith/runs/`
- [ ] Triage selects roles based on project context
- [ ] SkillRounds run with "what should be improved?" framing
- [ ] ConvergenceCheck produces ranked top 3 findings
- [ ] `WriteTicketsHandler` creates tickets with `agent-smith-autonomous` label
- [ ] No tickets written without convergence
- [ ] `WriteRunResult` logs the autonomous run
- [ ] Schedule trigger works
- [ ] Bootstrap (p22) prompts for project-vision.md
- [ ] `dotnet build` + `dotnet test` clean

## Dependencies

- p34 (Multi-Skill Architecture — SkillRounds mechanism)
- p22 (Bootstrap — project-vision.md created here)
- p41 (Decision Log — autonomous runs log decisions too)
- p61 (Knowledge Base — LoadRuns reads compiled wiki, not raw runs)

## Why This Is Different From Everything Else

Every other pipeline waits for a human to tell it what to do.

`autonomous` does not wait. It observes, understands, decides, and acts —
within its defined boundaries. The boundary today is: it writes tickets,
not code. That boundary can expand as trust grows.

This is what Agent Smith is named for.
