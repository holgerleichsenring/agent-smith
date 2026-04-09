# Phase Workflow

Agent Smith evolves through a structured phase process. Each phase is a bounded development increment with clear scope, dependencies, and acceptance criteria.

## What Is a Phase?

A phase is a Markdown document in `.agentsmith/phases/` that describes a feature, refactor, or capability addition. Phases are numbered sequentially (`p01`, `p02`, ..., `p62`).

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
    p01: "Initial pipeline: fetch ticket, checkout, plan, execute, commit"
    p02: "Retry and resilience: Polly policies, test retry loop"
    p52: "Single executable release: binaries for 5 platforms"
  active: {}
  planned:
    p23: "Multi-repo support"
```

## Implemented Phases

Agent Smith has completed over 60 phases covering:

- **Core pipeline** (p01-p10) -- ticket fetch, checkout, plan, execute, test, commit, PR
- **Resilience** (p02, p08) -- retry policies, error handling
- **Multi-provider** (p11, p40a-d) -- Claude, OpenAI, Gemini, Ollama support
- **Security stack** (p54-p56, p60) -- static scan, git history, dependency audit, ZAP, auto-fix, trend
- **API security** (p44-p48) -- Nuclei, Spectral, API specialist panel
- **Chat gateway** (p14-p18) -- Slack integration, Redis pub/sub, job spawning
- **Multi-skill** (p34-p36) -- role-based triage, skill rounds, convergence
- **Skill standard** (p57a-c) -- SKILL.md format, skill manager, autonomous pipeline
- **Interactive dialogue** (p58) -- structured Q&A across all channels
- **PR comments** (p59) -- webhook-based commands and dialogue
- **Knowledge base** (p61) -- wiki compilation, querying, linting

## Creating a New Phase

1. Write the phase document in `phases/planned/` with goal, approach, and definition of done
2. Move to `phases/active/` when starting implementation
3. Implement according to the document
4. Move to `phases/done/` when all acceptance criteria are met
5. Update `context.yaml` state

!!! info "Phase-first workflow"
    The phase document is always written **before** implementation starts. This ensures clear scope and prevents scope creep.
