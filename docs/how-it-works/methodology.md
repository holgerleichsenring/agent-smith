# Methodology

The first AI coding agents I tried would happily write code that referenced functions that didn't exist, or invent the third argument to a method when only two were defined. The fix everyone reached for was "give the LLM more context" — bigger window, RAG over the codebase, retrieval-augmented this and that. I got tired of that approach because the failure mode was the same: a confident wrong answer, dressed up better.

The way Agent Smith works is different. Every change has to come from evidence in the codebase, and every claim the agent makes can be challenged by a different role before any code lands. That's the methodology. The rest of this page is what it looks like in practice.

## The evidence contract

Every claim a role makes is a typed observation, not free text. Each observation carries a `Concern`, a `Confidence` (0–100), a `Blocking` flag, and an `EvidenceMode`. Evidence mode is the key idea:

- `AnalyzedFromSource` — the observation is backed by something the role actually read in the codebase (a file path plus a line number).
- `Potential` — the observation is the role thinking out loud about something that might be true but hasn't been verified.

`Blocking=true` observations with `Confidence<70` get auto-downgraded to non-blocking with a structured log entry. Speculation surfaces; speculation doesn't gate the pipeline. And the framework enforces the reading part: a scan finding that claims source analysis of a file the master never actually read gets downgraded to `Potential`.

## How a coding run flows

The coding pipelines (`fix-bug`, `add-feature`) used to be a fixed chain of separate phases. Today they're a master-based loop with the gates pulled out where you can see them:

**Expectation before plan.** After the agent has analyzed (and where possible reproduced) the problem, it writes down what the fix must achieve — observed behavior, expected assertions, constraints — and you ratify or edit it. That ratified expectation is the run's acceptance contract; see [Expectations](expectations.md).

**Plan before approval.** `GeneratePlan` produces a concrete plan, and the approval gate shows *that plan* — you approve something specific, and the master then executes the approved plan rather than planning from scratch behind the gate.

**One master, real verification.** The `coding-agent-master` skill plans the details, edits the code, and runs the repo's own build and tests itself via real commands, visible in the run timeline. There is no rigid framework-guessed test step — the repo's own test command is the truth.

**The keystone.** The framework refuses to call a fix/feature run successful unless code actually changed *and* verification came back green. And the gate is regression-aware: a test that was already red on the base branch doesn't block your fix; a green→red flip does. A red-verification run still pushes its branch and opens the PR as a draft — reviewable, honestly labeled.

## Scan pipelines: master plus roles

The findings pipelines (`security-scan`, `api-security-scan`, `legal-analysis`) keep the multi-role shape: a master orchestrates specialist roles, every role emits typed observations under the evidence contract, and the delivered result is the master's curated triage backed by the deterministic scanner facts. The scan master works on a read-only tool surface — it can read, list and grep, and that's it. Details per pipeline under [Reference → Pipelines](../reference/pipelines/index.md).

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

Expectation before plan: agree on the WHAT while it's still cheap to disagree. Every argument you have at ratification time is an argument you don't have on the PR.

Plan before code: the agent commits to an approach before changing anything, and the operator gets to see what it's going to do.

Verification with the code, by the same loop that wrote it: the master runs the repo's own tests as it works, so "done" and "green" are the same moment, not two steps that can drift apart.

Approval before code (with `--headless` to skip once you trust it): the operator's last off-ramp before code lands.

## Next

- [Lifecycle](lifecycle.md) — the same flow, visualised.
- [Multi-repo](multi-repo.md) — what changes when one run touches several repos.
- [Skills catalog](skills-catalog.md) — where the skill files live and how they version.
