# Autonomous Pipeline

The **autonomous** pipeline runs without a ticket and produces tickets. Agent Smith observes a project, understands what matters, and decides what should be improved next. The output is not code or a PR, but a set of tickets that reflect the agent's own assessment.

## How It Differs

Every other pipeline waits for a human to say what to do. The autonomous pipeline inverts this: the agent writes the tickets. The human decides whether to act on them.

```
Regular:     Human writes ticket --> Agent executes
Autonomous:  Agent observes --> Agent writes tickets --> Human reviews
```

## Pipeline Steps

| # | Command | What It Does |
|---|---------|--------------|
| 1 | CheckoutSource | Clones repo, creates working copy |
| 2 | BootstrapProject | Detects language, framework, build system |
| 3 | LoadContext | Loads context.yaml, code-map, coding-principles |
| 4 | LoadCodeMap | Generates LLM-navigable code map |
| 5 | LoadVision | Reads project-vision.md (what matters for this project) |
| 6 | LoadRuns | Reads last N runs: results, decisions, costs, findings |
| 7 | Triage | Selects which specialist roles are relevant |
| 8-10 | SkillRounds | Multi-agent analysis with convergence checking |
| 11 | WriteTickets | Creates tickets in the ticket provider |
| 12 | WriteRunResult | Logs the autonomous run |

## Specialist Roles

The same multi-skill mechanism as fix-bug and security-scan, but the question is different. Not "what is wrong with this code?" but "what should be improved in this project?"

| Role | Perspective |
|------|-------------|
| **Architect** | Code-map and architecture decisions. Is the structure still right? Are there drifting patterns? |
| **Developer** | Coding principles and recent PRs. Recurring principle violations? Low test coverage in hot areas? |
| **Product Owner** | Project vision and run history. Planned features never started? Tickets that keep reopening? |
| **Security** | Last security scan findings. Accepted but unfixed findings? Systemic patterns? |

Triage decides which roles are relevant. A pure backend project may skip the Product Owner. A project with no security history skips Security.

## Convergence

The roles must agree on the **top 3 things** this project needs. Not a list of everything that could be better -- three items, ranked, with reasoning.

If roles disagree, another round runs. If no consensus after the max round limit, the run produces a "no consensus" result and no tickets are written.

## project-vision.md

A human-written file that gives the autonomous pipeline context about what matters:

```markdown
# Project Vision

## Who uses this
Internal teams for order processing.

## What matters most
Reliability -- zero data loss on payment transactions.

## What is explicitly not wanted
UI changes. This is a backend-only API.

## Definition of good enough
Feature is complete when tests pass and the API contract is stable.
```

Created once during project setup (`agent-smith init`). Without it, the autonomous pipeline runs without the Product Owner role.

## Ticket Output

Each ticket created by the autonomous pipeline gets:

- **Title** -- the finding in one sentence
- **Body** -- reasoning from convergence, which roles agreed, confidence score
- **Label** -- `agent-smith-autonomous` (signals agent-written, not human-written)
- **Priority** -- derived from confidence and role agreement

## Configuration

```yaml
projects:
  my-api:
    pipeline: autonomous
    trigger:
      schedule: "0 8 * * 1"         # every Monday 8am
    autonomous:
      max_tickets: 3                 # don't flood the backlog
      min_confidence: 7              # only high-confidence findings
      lookback_runs: 10              # how many past runs to analyze
      roles: auto                    # triage decides (or explicit list)
      ticket_provider: github        # where to write tickets
```

## CLI

```bash
# Run autonomous analysis
agent-smith autonomous --project my-api

# Dry run -- show what would be suggested without creating tickets
agent-smith autonomous --project my-api --dry-run
```

## Learning Over Time

Each autonomous run produces a `WriteRunResult`. Over time, the agent learns:

- Which tickets were accepted vs. rejected by the human
- Which roles consistently find actionable items
- Which findings were dismissed (feeds back -- don't suggest this again)

The feedback loop improves ticket quality for each specific project.
