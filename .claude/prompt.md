# Agent Smith — Claude Code Instructions

## Context Files (read in this order)

1. `.agentsmith/context.yaml` — architecture, stack, integrations, phase status
2. `.agentsmith/coding-principles.md` — code quality rules (ALWAYS follow)
3. `.agentsmith/phases/active/p{NN}-*.md` — prompt for the phase being implemented

## Phase Directory Structure

```
.agentsmith/phases/
├── done/       # completed phases (historical reference)
├── active/     # phase currently being worked on (max 1)
└── planned/    # upcoming phases with requirements
```

## Implementation Workflow (follow this order for every phase)

1. **Write phase prompt first** — create `.agentsmith/phases/planned/p{NN}-slug.md` with requirements, scope, and file summary BEFORE writing any code. This is mandatory, no exceptions.
2. **Move to active** — move the phase file from `planned/` to `active/` when starting work
3. **Enter plan mode** — explore codebase, design approach, get user approval before coding
4. **Implement step by step** — contracts/models first, then implementation, then DI wiring, then tests
5. **Build after each step** — `dotnet build`, fix errors immediately
6. **Run ALL tests** — `dotnet test`, ensure 0 failures before moving on
7. **Update `.agentsmith/context.yaml`** — move phase from `planned`/`active` to `done`
8. **Move to done** — move the phase file from `active/` to `done/`
9. **Commit** — one commit per phase, descriptive message

## Key Rules

- **English only** — all code, comments, docs, exceptions, logs, prompts, commit messages
- **Conventions** — file-scoped namespaces, primary constructors, sealed classes, records for DTOs
- **No over-engineering** — only build what the phase requires, nothing more
- **Tests** — every new public method gets at least one test (AAA pattern)
