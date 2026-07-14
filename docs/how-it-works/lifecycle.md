# Lifecycle

What happens between "ticket lands in the tracker" and "ticket gets set back to resolved with PR links". The diagram first, then the prose.

![Lifecycle: ticket → orchestrator → sandboxes → pull requests → resolved](../assets/lifecycle.svg)

## In six steps

**1. The ticket comes in.** Webhook from your tracker, or a poll round picks it up, or a CLI invocation explicitly names it. The framework parses the payload and claims the ticket — the claim is a database lease, so a webhook+poll race can't double-trigger and one ticket has at most one live run, by construction. If the run's footprint doesn't fit right now it queues FIFO instead of failing ([capacity](../reference/operations/capacity.md)).

**2. The run reads before it provisions.** `FetchTicket` pulls the whole ticket — body, comment thread, image and document attachments. `ScopeRepos` then classifies which of the project's repos the ticket actually touches and narrows the run to that subset before a single sandbox exists. A five-repo project doesn't pay for five build boxes on a one-repo fix; if the master later finds it needs another repo after all, it can escalate mid-run (`ensure_repo_sandbox`).

**3. Sandboxes spawn, repos get cloned.** One sandbox per affected repo, each running the toolchain image the repo's stack calls for (declared in its `.agentsmith/context.yaml`, or pinned per language in config). The sandbox-agent binary is injected via an init-container that copies it into a shared `emptyDir`; the toolchain image itself stays unmodified. Branches `agentsmith/ticket-{N}` get cut from the default branch, private-registry credentials get pre-staged (`SetupRegistryAuth`), and `BootstrapGate` aborts early if the repo was never initialized.

**4. The expectation gets negotiated, the plan gets approved.** After analysis, `NegotiateExpectation` writes down what the fix must achieve — observed, expected, constraints — grounded in the analysis, and you ratify or edit it; the ratified expectation is the run's acceptance contract ([details](expectations.md)). Then `GeneratePlan` produces a concrete plan and the approval gate shows it *before* execution. A ticket too thin to plan from parks in `needs_clarification_status` with the open questions as a comment instead of guessing.

**5. The master executes.** One agentic loop (`coding-agent-master`), one conversation, N sandboxes routed by path prefix — file tool calls dispatch by the first path segment (`todolist-api/src/Auth.cs` → the `todolist-api` sandbox). The master edits, builds, and runs the repo's own tests itself; every command is visible in the run's timeline.

**6. The PRs open, the ticket gets resolved.** Commit + push + one PR per repo with changes, cross-linked to each other. Before anything is committed, the staged diff is scanned for known secret patterns — a leaked key refuses the commit. The run record (`plan.md` / `result.md` / `decisions.md`) is committed with the change. The ticket transitions to `done_status`, gets a comment with the PR URLs, and gets the `agent-smith:done` label. Success is a code guarantee: the framework refuses to report a fix/feature run successful unless code actually changed and verification came back green — and the verification gate is regression-aware, so a test that was already red on the base branch doesn't block your fix, while a green→red flip does.

## When it goes wrong

A run always finalizes — success, failure, timeout, cancel — and `result.md` leads with the why. The failure is captured in two places:

- The ticket gets the `agent-smith:failed` label (and your configured `failed_status`, if set) plus a comment with the failing step + error message.
- The run record exists with whatever got written before the failure, and the WIP branch is pushed best-effort so partial work isn't lost.

A PR from a run whose verification ended red opens as a draft — reviewable, clearly marked, never mistaken for a finished change.

Run liveness is derived from the orchestrator itself (does the run's pod/container actually exist), not from a heartbeat key that a busy process might miss. The database knows what was in flight; a server restart reconciles instead of duplicating. Analysis stays fresh the same way — the project-map cache is keyed on the repo's HEAD SHA, so a new commit re-analyzes instead of reasoning about last week's code.

## What if no skill matches

Triage picks the skill roster by evaluating activation expressions against the concept state (project language, pipeline, ticket fields). If zero skills match, the framework fails fast with a message that names the observed concept values and the available skills. This is intentional — silently falling back to a "generic" skill produced incoherent runs in earlier versions.

The fix is usually one of: declare the missing project language in `.agentsmith/context.yaml`, update to a release whose embedded catalog covers the case, or contribute a skill upstream.

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

- [Methodology](methodology.md) — why plan / execute / verify are ordered the way they are.
- [Expectations & durable dialogue](expectations.md) — the acceptance contract from step 4.
- [Multi-repo](multi-repo.md) — the path-prefix dispatch model, deeper.
- [Skills catalog](skills-catalog.md) — what the master and its sub-agents are built from.
