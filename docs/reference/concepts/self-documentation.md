# Self-documentation

Agent Smith doesn't just run tasks, it records why it was built the way it was. Every feature has a phase. Every phase has a rationale. Every run has a cost.

This is not documentation written after the fact. It is documentation produced as a side effect of the work itself.

## The three layers

### Layer 1: Phases (the "what" and "why")

Every capability in Agent Smith originated in a phase spec. Phases are YAML files in `.agentsmith/phases/` and move through `planned/` → `active/` → `done/`. Each spec states:

- The goal, in one sentence
- Which part of the system it applies to, and which earlier phases it requires
- The scope, the steps and the tests
- The definition of done

```
.agentsmith/phases/
├── done/
│   ├── p0001-core-infrastructure.yaml
│   ├── p0064-typed-skill-orchestration.yaml
│   ├── 2026-09-27-481bf-ticket-kind-is-a-word.yaml
│   └── ...
├── active/
│   └── 2026-09-28-057db-docs-current-state-voice.yaml
└── planned/
    └── ...
```

The older phases carry counter ids like `p0064`; newer ones carry an id minted from the date, like `2026-09-27-481bf`. See [Phase workflow](../architecture/phase-workflow.md) for how ids are minted and how a phase moves through the directories.

The `code` pipeline keeps the same record in the repositories it works on: every phase a run executes is written to that repository's `.agentsmith/phases/done/` and indexed in its `context.yaml`, and ships with the pull request.

### Layer 2: Runs (the "how much" and "what happened")

Every run leaves a directory under `.agentsmith/runs/<run-id>/` in the repository it worked on, with a `plan.md` and a `result.md`. The `result.md` carries machine-readable frontmatter:

```yaml
---
ticket: "#57 — GET /todos returns 500 when database is empty"
date: 2026-05-20
result: success
type: fix
duration_seconds: 252
run_id: 2026-05-20T22-27-43-8a3f
pipeline_name: code
tokens:
  input: 52196
  output: 12679
  cache_read: 30114
  total: 94989
cost:
  total_usd: 0.3400
---
```

Below the frontmatter it lists the changed files, the decisions, and the execution trail. Combined with the git diff, the full story of each execution is recoverable. See [Cost tracking](cost-tracking.md) for the cost fields.

### Layer 3: Decisions (the "why not")

Decisions are YAML files in `.agentsmith/decisions/`, one per phase (`decisions/<phase-id>.yaml`) or per run (`decisions/<run-id>.yaml`). Each entry records what was chosen and, in the phase files, what it was chosen over and why:

```yaml
phase: 2026-09-27-481bf

decisions:
  - category: Implementation
    chose: "The kind is the tracker's own word, with an icon only for the few every tracker means the same by"
    over: "A set of kinds of ours, drawn as icons"
    reason: |
      Process templates define their own work-item types, so a closed set over an open
      vocabulary either mislabels something or silently shows nothing.
```

## Why this matters

Most AI tools are black boxes. You don't know why they do what they do, how much it costs, or what they decided not to do.

Agent Smith is an audit trail. Six months after a pipeline ran, you can answer:

- **What did it change?** `result.md` lists the changed files
- **What did it cost?** Token usage and USD in the frontmatter
- **Why was it built that way?** The phase spec with its goal and scope
- **What alternatives were considered?** The decision file for that phase or run

## Exploring your project's history

```bash
# See all phases
ls .agentsmith/phases/done/

# See all runs
ls .agentsmith/runs/

# Find when a decision was made
grep -r "Repository" .agentsmith/decisions/

# Read the decisions behind one phase
cat .agentsmith/decisions/p0057a.yaml
```

## Related

- [Phases & runs](phases-and-runs.md): the lifecycle and structure of phases and runs
- [Decision logging](decisions.md): how the agent records its decisions during a run
- [Cost tracking](cost-tracking.md): token usage and cost analysis
