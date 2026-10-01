# Phase workflow

Agent Smith evolves through a structured phase process. Each phase is a bounded development increment with a clear goal, scope, dependencies and definition of done.

## What is a phase?

A phase is a YAML spec in `.agentsmith/phases/` that describes a feature, refactor, or capability addition. The file is named `{id}-{label}.yaml`, for example `2026-09-27-481bf-ticket-kind-is-a-word.yaml`.

```
.agentsmith/phases/
  done/              # Completed phases (historical reference)
  active/            # Currently in progress
  planned/           # Specified, not yet implemented
```

## Phase ids

A new phase id is minted from the clock: today's UTC date plus four random hex digits, `{yyyy-MM-dd}-{xxxx}`, for example `2026-08-24-8a3f`. Minting needs no knowledge of what anyone else has taken, so a worktree, an agent without network access and two agents working in parallel can all mint safely. The four hex digits make a same-day collision unlikely.

Phases cut from one piece of work form a series. They share one minted number and append a lowercase letter: `2026-08-24-8a3fa`, `2026-08-24-8a3fb`, `2026-08-24-8a3fc`. The letter is appended, not dashed, because a label may itself begin with a one-letter word. The base number of a series is not itself a phase, and a phase that turns up later mints its own number instead of extending the series.

Counter ids such as `p0042`, `p0057a` and `p0131c-pre` are a closed namespace. Every one of them stays valid and is never renamed, but no new ones are minted.

An id is frozen; the file name is a pointer. Every `requires:` edge, decision file and commit message cites the id, so moving or relabelling a phase file breaks nothing but the pointer to it in `context.yaml`.

## Phase labels

The label after the id is a topic of 2 to 5 words and at most 50 characters. It is area-first: the leading word names the subject area and the rest narrows it, as in `checkpoint-partial-restore` or `ticket-kind-is-a-word`, so related phases group together in a directory listing. The claim itself lives in the `goal:`, one sentence of at most 200 characters.

## Phase lifecycle

```
planned/  -->  active/  -->  done/
```

| Status | Directory | Meaning |
|--------|-----------|---------|
| Planned | `phases/planned/` | Specified with goal, scope and definition of done, not yet started |
| Active | `phases/active/` | Currently being implemented |
| Done | `phases/done/` | Implemented. The spec stays as historical reference |

## Phase spec structure

```yaml
phase: 2026-09-27-481bf
goal: "One sentence stating what the phase makes true."
applies_to: "Which part of the system the phase touches"
requires: ["2026-09-27-481bb", "2026-09-27-481bc"]
scope:
  - What is in and out of this phase
steps:
  - The implementation steps, in order
tests:
  - The tests that prove it
done:
  - "One line: an acceptance criterion that is true once the phase is finished"
  - given: "an optional precondition"
    when: "the trigger"
    then: "the observable result"
```

`phase` and `goal` are required; the schema is `.agentsmith/phase-spec.schema.json`. A `done` item is one line, or a scenario with `when` and `then` (and an optional `given`, nothing else) when the criterion has a trigger and an observable result. Every reader sees a scenario as one plain line — `GIVEN an optional precondition WHEN the trigger THEN the observable result` — in the run's acceptance contract, the execution prompt and the filed ticket alike. The reasoning behind the choices goes into a separate decision file, `.agentsmith/decisions/{id}.yaml`.

## Phase tracking

The `state` section of each context's `context.yaml` (`.agentsmith/contexts/<name>/context.yaml`) tracks the phases. A `done` entry is one index line of at most 400 characters: what shipped, and a pointer to the spec.

```yaml
state:
  done:
    2026-09-27-481bf: "A ticket says what kind its tracker calls it. -> .agentsmith/phases/done/2026-09-27-481bf-ticket-kind-is-a-word.yaml"
    p0001: "Initial pipeline: fetch ticket, checkout, plan, execute, commit -> .agentsmith/phases/done/p0001-core-infrastructure.yaml"
  active: {}
  planned: {}
```

## Creating a new phase

1. Mint an id and write the spec in `phases/planned/` with goal, scope and definition of done
2. Move it to `phases/active/` when starting implementation
3. Implement according to the spec, logging decisions in `decisions/{id}.yaml`
4. Move it to `phases/done/` when all acceptance criteria are met
5. Add the `state.done` line to `context.yaml`

!!! info "Phase-first workflow"
    The phase spec is always written **before** implementation starts. This ensures clear scope and prevents scope creep.

## Phases in your repositories

The `code` pipeline applies the same method to the repositories it works on. Each phase a run executes is written to that repository's `.agentsmith/phases/done/` and indexed with a `state.done` line in its `context.yaml`, so the target repository carries the same planned-to-done record. Those ids are minted from the ticket number plus a series letter: ticket `57` becomes `p0057a`, `p0057b`, and so on.
