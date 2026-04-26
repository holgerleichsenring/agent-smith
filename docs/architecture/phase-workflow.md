# Phase Workflow

Agent Smith evolves through a structured phase process. Each phase is a bounded development increment with clear scope, dependencies, and acceptance criteria.

## What Is a Phase?

A phase is a Markdown document in `.agentsmith/phases/` that describes a feature, refactor, or capability addition. Phases are numbered sequentially (`p0001`, `p0002`, ..., `p0062`).

```
.agentsmith/phases/
  done/              # Completed phases (historical reference)
  active/            # Currently in progress (max 1)
  planned/           # Specified, not yet implemented
```

## Phase Lifecycle

```
planned/  -->  active/  -->  done/
```

| Status | Directory | Meaning |
|--------|-----------|---------|
| Planned | `phases/planned/` | Specified with requirements and approach, not yet started |
| Active | `phases/active/` | Currently being implemented. Only one phase can be active |
| Done | `phases/done/` | Implemented. Document stays as historical reference |

## Phase Document Structure

```markdown
# Phase N: Title

## Goal
What we're building and why.

## Motivation
The problem this solves.

## Approach
Technical details of the implementation.

## Files to Create
- list of new files

## Files to Modify
- list of existing files to change

## Definition of Done
- [ ] Checklist of acceptance criteria

## Dependencies
Other phases that must be completed first.
```

## Phase Tracking

The `state` section in `context.yaml` tracks all phases:

```yaml
state:
  done:
    p0001: "Initial pipeline: fetch ticket, checkout, plan, execute, commit"
    p0002: "Retry and resilience: Polly policies, test retry loop"
    p0052: "Single executable release: binaries for 5 platforms"
  active: {}
  planned:
    p0023: "Multi-repo support"
```

## Implemented Phases

Agent Smith has completed over 60 phases covering:

- **Core pipeline** (p0001-p0010) -- ticket fetch, checkout, plan, execute, test, commit, PR
- **Resilience** (p0002, p0008) -- retry policies, error handling
- **Multi-provider** (p0011, p0040a-d) -- Claude, OpenAI, Gemini, Ollama support
- **Security stack** (p0054-p0056, p0060) -- static scan, git history, dependency audit, ZAP, auto-fix, trend
- **API security** (p0044-p0048) -- Nuclei, Spectral, API specialist panel
- **Chat gateway** (p0014-p0018) -- Slack integration, Redis pub/sub, job spawning
- **Multi-skill** (p0034-p0036) -- role-based triage, skill rounds, convergence
- **Skill standard** (p0057a-c) -- SKILL.md format, skill manager, autonomous pipeline
- **Interactive dialogue** (p0058) -- structured Q&A across all channels
- **PR comments** (p0059) -- webhook-based commands and dialogue
- **Knowledge base** (p0061) -- wiki compilation, querying, linting

## Creating a New Phase

1. Write the phase document in `phases/planned/` with goal, approach, and definition of done
2. Move to `phases/active/` when starting implementation
3. Implement according to the document
4. Move to `phases/done/` when all acceptance criteria are met
5. Update `context.yaml` state

!!! info "Phase-first workflow"
    The phase document is always written **before** implementation starts. This ensures clear scope and prevents scope creep.
