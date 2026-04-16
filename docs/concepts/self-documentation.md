# Self-Documentation

Agent Smith doesn't just run tasks — it records why it was built the way it was. Every feature has a phase. Every phase has a rationale. Every run has a cost.

This is not documentation written after the fact. It is documentation produced as a side effect of the work itself.

## The Three Layers

### Layer 1: Phases (the "what" and "why")

Every capability in Agent Smith originated in a phase document. Phases live in `.agentsmith/phases/` and move through `planned/` → `active/` → `done/`. Each document contains:

- The problem being solved
- Design decisions and alternatives considered
- Files to create and modify
- Definition of done

Example: Phase 64 introduced typed skill orchestration because free-form discussion between skills produced too much noise and consumed too many tokens. The decision — and its reasoning — is permanently recorded.

```
.agentsmith/phases/
├── done/
│   ├── p01-core-infrastructure.md
│   ├── p06-retry-resilience.md
│   ├── p34-multi-skill.md
│   ├── p54-91-pattern-scanner.md
│   ├── p55-findings-compression.md
│   └── p64-typed-skill-orchestration.md
├── active/
│   └── (max 1 at a time)
└── planned/
    └── p66-docs-enhancement.md
```

### Layer 2: Runs (the "how much" and "what happened")

Every pipeline execution produces a `result.md` with machine-readable frontmatter:

```yaml
---
run: r0047
pipeline: security-scan
project: my-api
branch: main
duration: 4m 12s
cost: $0.34
llm_calls: 9
tokens_in: 52196
tokens_out: 12679
findings: 16
---
```

The run result captures which commands ran, in what order, what the token usage was per step, and what the pipeline produced. Combined with the git diff, the full story of each execution is recoverable.

### Layer 3: Decisions (the "why not")

`decisions.md` captures architectural choices with alternatives considered and outcomes tracked. Not what was done — why it was chosen over alternatives.

```markdown
## Use deterministic skill graph instead of LLM triage

**Date:** 2026-03-15
**Phase:** p64
**Choice:** Build execution graph from skill metadata (runs_after/runs_before)
**Alternatives:**
  - LLM-based triage (status quo) — flexible but expensive and non-deterministic
  - Hardcoded pipeline order — simple but not extensible
**Outcome:** ~80% token reduction, reproducible execution order, no triage LLM call
```

## Why This Matters

Most AI tools are black boxes. You don't know why they do what they do, how much it costs, or what they decided not to do.

Agent Smith is an audit trail. Six months after a pipeline ran, you can answer:

- **What did it find?** → `result.md` with findings list
- **What did it cost?** → Token usage and USD in frontmatter
- **Why was it built that way?** → Phase document with rationale
- **What alternatives were considered?** → `decisions.md`

## Exploring Your Project's History

```bash
# See all phases
ls .agentsmith/phases/done/

# See all runs
ls .agentsmith/runs/

# Find when a decision was made
grep -r "Repository Pattern" .agentsmith/

# See what changed between phases
diff .agentsmith/phases/done/p54-*.yaml .agentsmith/phases/done/p55-*.yaml
```

## Related

- [Phases & Runs](phases-and-runs.md) — the lifecycle and structure of phases and runs
- [Decision Logging](decisions.md) — how architectural decisions are captured
- [Cost Tracking](cost-tracking.md) — token usage and cost analysis
