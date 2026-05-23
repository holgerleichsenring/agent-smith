# Lifecycle

What happens between "ticket lands in the tracker" and "ticket gets set back to resolved with PR links". The diagram first, then the prose.

![Lifecycle: ticket → orchestrator → sandboxes → pull requests → resolved](../assets/lifecycle.svg)

## In five steps

**1. The ticket comes in.** Webhook from your tracker, or a poll round picks it up, or a CLI invocation explicitly names it. The framework parses the payload, claims the ticket (SETNX in Redis so a webhook+poll race can't double-trigger), and enqueues a `PipelineRequest`.

**2. The orchestrator spawns sandboxes.** One sandbox per repo in the project, eager (all up before the first sandbox-requiring command). Each sandbox runs the toolchain image picked per repo by `SandboxSpecBuilder` — `.NET` repos get `dotnet/sdk:8.0`, a Node repo gets `node:20`, a Python repo gets `python:3.12`. The sandbox-agent binary is injected via an init-container that copies it into a shared `emptyDir`. The toolchain image itself stays unmodified.

**3. Each sandbox clones its repo.** `CheckoutSourceHandler` issues a `git clone` step into each sandbox over Redis. Branches `agentsmith/ticket-{N}` get cut from the default branch.

**4. The agent runs.** One plan, one agent conversation, N sandboxes routed by path prefix. File tool calls dispatch by the first path segment (`todolist-api/src/Auth.cs` → `Sandboxes["todolist-api"]`). The four-phase methodology (plan / agentic step / review / final) executes inside that conversation.

**5. The PRs open, the ticket gets resolved.** `CommitAndPRHandler` commits + pushes + opens one PR per repo with changes. Each PR body carries a `<!-- agentsmith:sibling-prs -->` marker. `PrCrossLinkHandler` then PATCHes each PR body to insert links to the sibling PRs. `WriteRunResult` writes `plan.md` / `result.md` / `decisions.md` into the run directory and updates `context.yaml`. The ticket transitions to `done_status`, gets a comment with the PR URLs, and gets the `agent-smith:done` label.

## When it goes wrong

If a step fails, the failure is captured in two places:

- The ticket gets the `agent-smith:failed` label and a comment with the failing step + error message.
- The run directory exists with whatever got written before the failure. `result.md` has the failure detail.

The PR — if `CommitAndPR` got far enough — stays open in draft so you can pick up where the agent left off. If `CommitAndPR` failed before pushing, no branch exists yet.

Stale jobs (orchestrator died mid-run, no heartbeat in Redis) get reconciled. `StaleJobDetector` notices the missing heartbeat, transitions the ticket back to `Pending`, the `EnqueuedReconciler` re-enqueues. The run gets restarted from scratch.

## What if no skill matches

Triage picks the skill roster by evaluating activation expressions against the concept state (project language, pipeline, ticket fields). If zero skills match, the framework fails fast with a message that names the observed concept values and the available skills. This is intentional — silently falling back to a "generic" skill produced incoherent runs in earlier versions.

The fix is usually one of: bump the skills catalog version, declare the missing project language in `.agentsmith/context.yaml`, or contribute a skill upstream.

## Per-repo bootstrap

When a project's repos haven't been bootstrapped yet (no `.agentsmith/context.yaml` in the repo), `BootstrapGate` aborts the run with a clear message: "run `init-project` first". The `init-project` pipeline iterates the project's repos, writes a `.agentsmith/context.yaml` and a `.agentsmith/coding-principles.md` into each, opens one bootstrap PR per repo, cross-links them. Run it once per project (it handles all the repos in one go).

## Run identifiers

Every run gets an id of the shape `{yyyy-MM-ddTHH-mm-ss}-{4hex}-{slug}`. ISO-8601 timestamp in UTC, 4-hex random suffix (kills same-second collisions when a batch of tickets all queue at once), human-readable slug at the end. Lexicographically sortable in `ls`. Stored in `context.yaml` under the top-level `runs:` map; the directory name on disk uses the full identifier.

```
.agentsmith/runs/
├── 2026-05-22T14-03-11-9f2a-fix-login-bug/
├── 2026-05-22T14-29-44-4c19-add-export-csv/
└── 2026-05-22T15-04-02-b7d1-security-scan/
```

## Next

- [Methodology](methodology.md) — what the agent actually does in step 4.
- [Multi-repo](multi-repo.md) — the path-prefix dispatch model, deeper.
- [Skills catalog](skills-catalog.md) — what triage is picking from.
