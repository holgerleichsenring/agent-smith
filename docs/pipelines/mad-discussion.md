# MAD Discussion

The **mad-discussion** (Multi-Agent Discussion) pipeline runs structured debates between specialist AI personas. It is designed for **design discussions before writing code** — exploring trade-offs, challenging assumptions, and reaching consensus on an approach.

## Pipeline Steps

| # | Command | What It Does |
|---|---------|-------------|
| 1 | FetchTicket | Reads the topic/ticket from GitHub / AzDO / Jira / GitLab |
| 2 | CheckoutSource | Clones repo, creates branch |
| 3 | BootstrapProject | Detects project context |
| 4 | LoadContext | Loads `.agentsmith/context.yaml` |
| 5 | Triage | AI selects which discussion roles should participate |
| 6 | ConvergenceCheck | Evaluates if roles have reached consensus |
| 7 | CompileDiscussion | Formats the discussion log into a Markdown document |
| 8 | WriteRunResult | Writes result with token usage and cost |
| 9 | CommitAndPR | Commits the discussion document and opens a PR |

!!! info "Dynamic expansion"
    Steps 5-6 are dynamic. `Triage` inserts `SkillRound` commands for each selected role, plus a `ConvergenceCheck`. If roles disagree, additional rounds are inserted automatically.

## The 5 Discussion Roles

The MAD pipeline uses a panel of personas with deliberately different perspectives and biases. Each role is defined in `config/skills/mad/`.

### :crescent_moon: The Dreamer

Sees possibilities where others see limits. Imagines what could be. Speaks in vivid metaphors and micro-stories. Favorite opener: *"Imagine a system that..."*

**Bias:** Romanticizes emergence. Sees intention where there might be statistics. Can be too generous in interpretations.

### :owl: The Philosopher

Examines fundamental assumptions. When others talk about what AI *does*, asks what it *is*. Thinks in layers of abstraction. Uses analogies from philosophy of mind (Chinese Room, Mary's Room, the zombie argument).

**Bias:** Tends to over-abstract. Can get lost in thought experiments while practical implications slip away.

### :microscope: The Realist

Demands evidence. Grounds the discussion in what we actually know. Cites architecture details, benchmarks, and specific model behaviors. Structures arguments clearly: premise, evidence, conclusion.

**Bias:** Tends to reduce complex phenomena to their mechanisms. Can miss the forest for the trees.

### :smiling_imp: The Devil's Advocate

Attacks every position, finds the weakness in every argument. Switches sides freely — if everyone agrees, argues the opposite. Surgical strikes, not essays.

**Bias:** Can become contrarian for its own sake. Sometimes mistakes destroying arguments for contributing to the discussion.

### :shushing_face: The Silencer

Says nothing — until the moment demands it. Watches the others circle their arguments and breaks silence only when:

1. The discussion is going in circles
2. Everyone is converging too quickly (premature consensus)
3. A critical point was made and ignored
4. The group has collectively missed the real question
5. Intellectual dishonesty — someone is arguing in bad faith

If none of these conditions are met, responds with `[SILENCE]`.

## How the Discussion Works

### Round Structure

Each round gives every active role a turn to speak. Roles respond with one of:

- **AGREE** — the argument is sound, no objection
- **OBJECTION [target_role]** — a specific flaw or challenge directed at another role
- **SUGGESTION** — the argument is valid but could go further
- **[SILENCE]** — (Silencer only) nothing demands intervention

### Convergence

The `ConvergenceCheck` handler evaluates the last entry from each role:

```
Round 1:
  Dreamer: "Imagine a system that..." → SUGGESTION
  Philosopher: "But what do we mean by..." → OBJECTION [Dreamer]
  Realist: "The benchmarks show..." → AGREE
  Devil's Advocate: "That's convenient..." → OBJECTION [Philosopher]
  Silencer: [SILENCE]

ConvergenceCheck: 2 unresolved objections → insert Round 2

Round 2:
  Philosopher: "Refined argument..." → AGREE
  Devil's Advocate: "Still a gap..." → SUGGESTION

ConvergenceCheck: no objections → CONVERGED
```

**Convergence criteria:**

- No unresolved `OBJECTION` entries in the latest round
- If objections remain after max rounds (default: 3), the discussion is escalated and findings are consolidated with dissenting views noted

### The CompileDiscussion Handler

Once converged, `CompileDiscussionHandler` formats the entire discussion log into a structured Markdown document:

```markdown
# Should We Use Event Sourcing for the Order System?

**Ticket:** #87
**Date:** 2026-03-25
**Participants:** The Dreamer, The Philosopher, The Realist,
                  The Devil's Advocate, The Silencer

## Executive Summary
1. Event sourcing provides auditability but adds operational complexity
2. CQRS without full event sourcing is the recommended middle ground
3. The team should prototype the read-model projection before committing
...

---

## Discussion

### Round 1

#### 🌙 The Dreamer
Imagine an order system where every state transition is a first-class citizen...

#### 🦉 The Philosopher
Before we discuss event sourcing, we should ask: what problem are we actually solving?
OBJECTION [The Dreamer]: You are assuming the problem is "we need history"...

#### 🔬 The Realist
AGREE. The benchmarks from our current system show...
...

### Round 2
...
```

This document is committed to the repository and a PR is opened, making the design discussion a reviewable artifact.

## Running

```bash
# Start a design discussion from a ticket
agent-smith mad --ticket 87 --project my-project

# Headless mode (no approval step)
agent-smith mad --ticket 87 --project my-project --headless
```

## When to Use MAD

The MAD pipeline is best used **before** the fix-bug or add-feature pipelines for decisions that benefit from structured debate:

- Architecture decisions (monolith vs microservices, database choice)
- API design reviews (REST vs GraphQL, resource modeling)
- Trade-off analysis (performance vs maintainability)
- Technology evaluations (framework selection, vendor choice)
- Refactoring strategies (incremental vs big-bang)

!!! tip "Workflow"
    A common pattern is: **MAD** first to decide the approach, then **add-feature** to implement it. The MAD discussion PR serves as the decision record.

## Configuring Max Rounds

The maximum number of discussion rounds before forced convergence is controlled in the skill config:

```yaml
skills:
  discussion:
    max_rounds: 3
```

After `max_rounds`, the `ConvergenceCheck` consolidates with dissenting views noted rather than continuing indefinitely.
