# Branch Persistence

Every ticket gets one work branch on the source remote, and everything about the ticket lives there: the approved specification, the code the runs write, and the partial work of a run that failed. A pod that restarts mid-run loses its `/tmp` working tree, and a second run on the same ticket starts in a fresh sandbox. The branch is how the second run sees what the first one did, instead of re-running every analyzer call from scratch — which is both expensive (tokens) and non-idempotent for any LLM round.

## Branch naming

The work branch is `agent-smith/{ticketId}`, for example `agent-smith/18693`. The name is **stable**: the same ticket produces the same branch every time, so the branch is cut once per ticket and every later run on that ticket reuses it. Composition lives in `TicketBranchNamer` (`AgentSmith.Application.Services`), a static helper.

A run that is handed a branch instead of composing one from its ticket (a pull-request review works on the PR's head branch, a scan on the branch it was asked to scan, project initialisation on `agentsmith/init`) uses that branch and leaves its base alone.

## The specification on the ticket branch

When a [spec dialogue](../../how-it-works/spec-dialogue.md) files a phase or an epic, the approved specification is written to the ticket's branch before any run starts, under `.agentsmith/specs/<provider>-<ticketId>/` with a `set.yaml` index. If the branch doesn't exist yet, it is created at the head of the default branch. The write is an ordinary commit and never forces.

The run that claims the ticket reads its specification from there. If the branch carries nothing (the write failed at filing time, say) and the server holds the approval record, the run publishes the set to the branch from that record. With neither, the ticket is parked, and the park names the path it found empty.

## Cutting and reusing the branch

Each repository is cloned fresh into the run's sandbox, and the run checks out the work branch:

1. If `agent-smith/{ticketId}` exists on the remote, the run checks it out and continues from it.
2. Otherwise it resolves the branch's base and cuts `agent-smith/{ticketId}` from it. The base is the clone's default branch (`origin/HEAD`), or the parent's branch for a ticket with a parent (see [Parent branches](#parent-branches)).

The base is resolved per repository, and the same base is used for three things: cutting the branch, merging newer base commits, and opening the pull request.

### Catching up with the base

A reused work branch may be behind its base: other work was merged while this ticket was parked or failed. Before the run does anything else it merges the base into the work branch (a merge, never a rebase).

If that merge conflicts, the run stops. The merge is aborted, nothing is pushed, and the failure names the conflicting paths:

```
merging 'origin/main' into 'agent-smith/18693' conflicts in 2 path(s): src/Api/TodoController.cs, src/Api/Startup.cs.
The merge was aborted, so the branch is unchanged — resolve the conflict on 'agent-smith/18693',
or delete it to start again from 'origin/main'.
```

### The pull request

The pull request goes against the base its branch was cut from: the default branch, or the parent's branch. If a pull request for the branch already exists against a different base, it is moved to the right one. That works on GitHub, GitLab and Azure Repos; a local repository opens no pull request.

## Parent branches

A ticket labelled `phase-parent:<id>` (see [labels](../../trigger-it/labels.md#phase-tickets)) is one slice of a larger piece of work. Its branch is cut from the parent's branch, `agent-smith/<id>`, instead of from the default branch, and its pull request targets the parent's branch.

If the parent's branch doesn't exist yet in a repository, the first run that needs it publishes it there, at the default branch's head. The push only creates; it never overwrites. When two runs race to publish it, the one that loses adopts the branch the other one pushed. That happens once per repository.

The run also reads the parent ticket's text as background for deriving its own specification, capped at 20,000 characters with the opening kept. If the parent can't be read, the run says so and proceeds on its own ticket.

## The persist path

When a step fails, `PipelineErrorHandler` posts the failure comment on the ticket, then pushes the working tree to the work branch as a `[wip]` commit, then marks the ticket Failed. Persisting runs in its own try/catch — a persist failure never masks the original failure. It is skipped when the run failed before checkout (there is no working tree) and for pipelines that don't change code.

`PersistWorkBranchHandler` works per repository, in that repository's sandbox:

1. Checks for working changes and stages them with `git add -A`. Nothing staged means `NoChanges`.
2. Commits with the message `[wip] agent-smith run {runId}` and three trailers (`Run-Id`, `Pipeline`, `Failed-Step`), so the commit is searchable from a log line.
3. Pushes with `--force-with-lease`. If the lease is stale, it fetches and tries once more.

The results of all repositories are combined; the worst one decides.

### Failure kinds

| Kind | Trigger | Operator action |
|------|---------|-----------------|
| `NoChanges` | Nothing to commit | Informational — the step failed before producing any file changes; nothing to persist |
| `AuthDenied` | Push rejected with 401/403, "authentication" or "unauthorized" | Check the source-provider PAT/credentials; the run's output is still in the logs |
| `RemoteDivergent` | Push rejected (`non-fast-forward` or `rejected`) | The remote branch moved under the run; look at the branch on the remote and decide what to keep |
| `NetworkBlip` | `HttpRequestException` during the push | Transient; the partial work of this run is lost, re-trigger the ticket |
| `Unknown` | Anything else | Inspect the log for the underlying exception |

Persist failures are logged at `Error` (or `Warning` for `NetworkBlip`) next to the original failure, not in place of it.

## What this does not protect

- **State that never reached disk.** Persistence happens at step boundaries. A handler that holds partial state in memory loses it.
- **A successful run.** Persistence runs only on the failure path. Successful runs commit and push as part of `CommitAndPRCommand`.
- **Failures before checkout.** With no working tree there is nothing to persist.

## Related

- [Ticket Lifecycle](ticket-lifecycle.md) — where persist sits in the InProgress → Failed transition
- [Pipeline System](pipeline-system.md) — command/handler model and failure path
- [Cost Tracking](cost-tracking.md) — why preserving partial work matters for token budgets
