# Methodology

The first AI coding agents I tried would happily write code that referenced functions that didn't exist, or invent the third argument to a method when only two were defined. The fix everyone reached for was "give the LLM more context" — bigger window, RAG over the codebase, retrieval-augmented this and that. I got tired of that approach because the failure mode was the same: a confident wrong answer, dressed up better.

The way Agent Smith works is different. Every change has to come from evidence in the codebase, and every claim the agent makes can be challenged by a different role before any code lands. That's the methodology. The rest of this page is what it looks like in practice.

## The four phases

Every structured pipeline (`fix-bug`, `add-feature`, `security-scan`, `api-security-scan`) runs the same four phases. The roles change, the prompts change, the order doesn't.

### Plan

Picks the roles. One **Lead** for the phase plus a handful of **Analysts**. Each contributes observations — typed JSON, not free-text. Each observation carries a `Concern`, a `Confidence` (0–100), a `Blocking` flag, and an `EvidenceMode`. Evidence mode is the key idea here:

- `AnalyzedFromSource` — the observation is backed by something the role actually read in the codebase (a file path plus a line number).
- `Potential` — the observation is the role thinking out loud about something that might be true but hasn't been verified.

`Blocking=true` observations with `Confidence<70` get auto-downgraded to non-blocking with a structured log entry. Speculation surfaces; speculation doesn't gate the pipeline.

At the end of the phase, the Lead's typed observations are written into the `PlanArtifact` and threaded into the system prompt for everything downstream.

### AgenticStep

Only in `fix-bug` / `add-feature`. The developer agent writes the code, iterating with tools (read, edit, run_command, grep, etc.). The plan from the previous phase is in its system prompt; the agent compares its changes against the plan as it works.

The `read_file` / `grep_in_tree` / `find_files` tools are bounded — head limits, size caps, timeout limits — to prevent the agent from drowning itself in irrelevant context. The bounds are in `Sandbox.Wire/SizeLimits.cs`; sensible defaults.

### Review

A different set of roles — **Reviewers** — reads the diff and compares it against the plan. Same observation contract; same evidence rule. A reviewer is required to cite a file:line if it's blocking. "This looks suspicious" with no evidence becomes a Potential, non-blocking, surfaces in the final report but doesn't fail the run.

### Final

One **Filter** role aggregates the per-skill observations into the run's output. For findings-style pipelines (`security-scan`) that's a deduped JSON list. For action pipelines (`fix-bug`) that's a synthesized summary that goes into the PR description and the ticket comment.

## Spec-first

The skills themselves are spec-first too. Every skill in the `agent-smith-skills` catalog is a YAML frontmatter + Markdown body, declaring:

- Which roles it supports (Lead / Analyst / Reviewer / Filter).
- Which phases it activates in.
- An activation expression — when does this skill apply to a ticket? (`project_language = "csharp"`, `pipeline = "security-scan"`, etc.)
- The prompts per role.

When triage picks a roster for a ticket, it's evaluating activation expressions against the concept state of the run (project language, tracker type, pipeline name, ticket fields). No hand-coded mapping from "this kind of ticket" to "these skills". The framework filters and the LLM picks the Lead from what's left.

## What lands in the run directory

Every run writes three files to `.agentsmith/runs/{run-id}/`:

**`plan.md`** — the plan after the Plan phase, role by role, with the typed observations rendered as bullet points. This is the why-record: six months later, when you're staring at a PR that closed ticket #4471 and wondering "why on earth did we pick path A", `plan.md` has the answer.

**`result.md`** — what got done. Files changed, PR URLs, ticket update. Plus the cost block:

```
Cost
  Total: $1.42 (1,041,231 tokens)
  Plan:    $0.18  (135k tokens)
  Execute: $0.91  (623k tokens)
  Review:  $0.27  (210k tokens)
  Final:   $0.06  ( 72k tokens)
```

If `pricing` is configured in the agent block, dollar costs are accurate. Otherwise just token counts.

**`decisions.md`** — non-obvious choices the agent made during the run. "Picked `400 BadRequest` over `404 Not Found` because the OpenAPI spec already documents 400 for malformed input". The agent writes these via the `log_decision` tool when something would surprise a future reader.

These three files plus the `runs:` index in `.agentsmith/context.yaml` are everything the [knowledge-base feature](../reference/concepts/knowledge-base.md) compiles when it builds the wiki across all your runs.

## Multi-role conversations

For the discussion-flavored pipelines (`mad-discussion`, `legal-analysis`) the same four-phase contract holds, but the AgenticStep is replaced with iterative rounds. Each role contributes observations per round; `ConvergenceCheckHandler` looks at the aggregated observations and decides whether to run another round (capped at 3 by default — runaway debates don't help anyone).

## What about hallucinations

The evidence-mode contract is the main mitigation. A blocking finding without a file:line citation is by definition a `Potential` observation; the framework auto-downgrades it. The agent can still be wrong about the file:line it points at — but you can check, which is a much weaker class of failure than "the agent invented a function name".

The other mitigation is the role separation. The Lead's plan and the agent's diff are evaluated by Reviewers that didn't see the Lead's reasoning, only the plan. Disagreement between Lead and Reviewer shows up in `result.md`.

## Why this order

Plan before code: the agent commits to an approach before changing anything, and the operator gets to see what it's going to do.

Code before review: a reviewer with the diff in hand can argue about something concrete. A reviewer arguing against a plan is theatre.

Review before final: the final summary should reflect the review's conclusions, not the plan's intentions.

Approval before code (with the optional `--auto-approve` to skip): the operator's last off-ramp before code lands.

## Next

- [Lifecycle](lifecycle.md) — the same flow, visualised.
- [Multi-repo](multi-repo.md) — what changes when one run touches several repos.
- [Skills catalog](skills-catalog.md) — where the skill files live and how they version.
