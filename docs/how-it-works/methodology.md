# Methodology

The first AI coding agents we tried would happily write code that referenced functions that didn't exist, or invent the third argument to a method when only two were defined. The fix everyone reached for was "give the LLM more context" — bigger window, RAG over the codebase, retrieval-augmented this and that. We got tired of that approach because the failure mode was the same: a confident wrong answer, dressed up better.

The way codeably works is different. Every change has to come from evidence in the codebase, and every claim the agent makes can be challenged by a different role before any code lands. That's the methodology. The rest of this page is what it looks like in practice.

## The evidence contract

Every claim a role makes is a typed observation, not free text. Each observation carries a `Concern`, a `Confidence` (0–100), a `Blocking` flag, and an `EvidenceMode`. Evidence mode is the key idea:

- `AnalyzedFromSource` — the observation is backed by something the role actually read in the codebase (a file path plus a line number).
- `Potential` — the observation is the role thinking out loud about something that might be true but hasn't been verified.

Speculation surfaces; it doesn't get to pass as fact. And the framework enforces the reading part: a finding that claims source analysis of a file the master never actually read gets downgraded to `Potential`.

## How a coding run flows

There is one coding pipeline, `code`. Its shape is simple to say: the ticket becomes a specification, and the specification is worked spec by spec, each spec checked before the next one starts.

**The spec is the plan.** After the code has been read, `DeriveSpec` cuts the ticket into an ordered set of specs on the ticket branch — goal, steps, "done when" per spec. Before it writes, the derivation looks at the repository through read-only tools, and the cut it posts to the ticket separates facts (each backed by a look it took) from assumptions (stated without one). When several specs share names, data or files, the series carries a contract that says who owns what, and a cut that collides is sent back. There is no second planning step and no approval gate: the master opens on the spec, and the spec is what gets verified. Details in [Expectations](expectations.md).

**Hand it back instead of guessing.** A ticket that reads two ways, contradicts the repository, or can't be built as written is handed back at derivation, before any code. A ticket that asks for something that must not be done is refused even earlier, by the scope call, before a sandbox exists.

**One master, real commands.** The `coding-agent-master` skill leads the spec's work: the code is edited and the repo's own build and tests are run via real commands, visible in the run timeline.

**A master that checks, a sub-agent that builds.** The master briefs a sub-agent with everything it needs, looks at what it built — the diff, the build and tests, the app where the sandbox can run it — sends work back until it holds, and ends with [its report](delivery-account.md) in markdown — what it delivered and how it saw it, what it fixed in passing, what is open and why, the follow-up specs it proposes — and a `finish_spec` call with the status and follow-ups the pipeline acts on. `VerifySpec` then runs the verify stages each repository declares; a red stage goes back to the master while its repairs make progress, and only a stage that stays red the same way stops the spec.

A run that stops half way still delivers honestly: verified specs ship on a ready PR with a "Not delivered" section, unverified work is reset, and a run that verified nothing leaves a draft PR with a failure banner. See [Lifecycle](lifecycle.md#when-it-goes-wrong).

## Scan pipelines: master plus roles

The findings pipelines (`security-scan`, `api-security-scan`, `legal-analysis`) run a master that orchestrates specialist roles; every role emits typed observations under the evidence contract, and the delivered result is the master's curated triage backed by the deterministic scanner facts. The scan master works on a read-only tool surface — it can read, list and grep, and that's it. Details per pipeline under [Reference → Pipelines](../reference/pipelines/index.md).

## Spec-first

The methodology works on two levels. The runs follow it: every change starts as a spec with a "done when" list, and ends as a spec record committed to the target repository under `.agentsmith/specs/done/`, with a one-line entry in that repository's context. The skills follow it too. Every skill in the `agent-smith-skills` catalog is YAML frontmatter plus a Markdown body, pinned by version, so the judgement a master exercises can be tuned without a release. See the [Skills catalog](skills-catalog.md).

## What lands in the run directory

Every run leaves a record in the repository, committed with the change. The run directory is `.agentsmith/runs/{run-id}/`:

**`result.md`** — what got done. YAML frontmatter with the result, the duration, token counts and the cost block, then the changed files, the masters' reports, decisions and the execution trail. A failed run leads with why. If `pricing` is configured for the agent, dollar costs are accurate; otherwise you get token counts.

**`plan.md`** — the master's own working plan for the run, when it wrote one.

**Decisions** — non-obvious choices the agent made during the run. "Picked `400 BadRequest` over `404 Not Found` because the OpenAPI spec already documents 400 for malformed input". The agent records these with the `log_decision` tool when something would surprise a future reader; they land in `.agentsmith/decisions/<run-id>.yaml` and in the run's event stream.

The specification itself lives next to the code: one YAML spec and one Markdown companion per spec under `.agentsmith/specs/planned/`, moved to `.agentsmith/specs/done/` once it ran, and one manifest per series at `.agentsmith/series/{base}.yaml`, which also records where every part of the ticket went. The run directories are what the [knowledge-base feature](../reference/concepts/knowledge-base.md) compiles when it builds the wiki across all your runs.

## What about hallucinations

The evidence contract is the first mitigation. A finding without a file:line citation is a `Potential` observation by definition, and a claimed citation of a file nobody read is downgraded. The agent can still be wrong about the file:line it points at — but you can check, which is a much weaker class of failure than "the agent invented a function name".

The second is role separation. The instance that judges the work is never the one that did it: the master that checks a spec did not write its code — it looks at what its sub-agent built instead of asking it. Asking the author whether it is done is persuasion; looking at the branch is not.

## Why this order

Spec before code: agree on the WHAT while it's still cheap to disagree. Every correction you make to the cut is an argument you don't have on the PR.

Verification after every spec, not once at the end: each spec ends at its own done-list, so stopping is structural rather than a judgement the model has to reach, and a run that stops half way knows exactly which specs are through.

Review after green: the review reads verified work, so what it objects to is quality, not whether the thing works.

## Next

- [Lifecycle](lifecycle.md) — the same flow, step by step.
- [How a spec is checked](delivery-account.md) — the master's report and the build stages.
- [Multi-repo](multi-repo.md) — what changes when one run touches several repos.
- [Skills catalog](skills-catalog.md) — where the skill files live and how they version.
