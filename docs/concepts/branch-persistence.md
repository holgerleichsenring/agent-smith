# Branch Persistence

Pipeline runs do real work — generated plans, agentic edits, test runs. When a pod restarts mid-pipeline, that work is on a `/tmp` working tree that the next pod cannot see. Without intervention, the next attempt would re-run every analyzer call from scratch, which is both expensive (tokens) and non-idempotent for any LLM round.

Branch persistence is the framework's mitigation: every ticket gets a deterministic work-branch on the source remote, and the failure path of every pipeline pushes the working tree to that branch as a `[wip]` commit before the lifecycle is marked Failed.

## Branch naming

The work-branch name is derived from the ticket id. It is **stable** — the same ticket on the same source produces the same branch every time, so a re-run can find the previous attempt's state.

| Form | When it applies | Example |
|------|-----------------|---------|
| `agent-smith/{ticketId}` | One-repo-per-ticket-system deployments (the AAD-DEV pattern, no project disambiguation needed) | `agent-smith/18693` |
| `agent-smith/{platform}/{projectSlug}/{ticketId}` | Multi-platform / multi-project deployments where ticket ids may collide across systems | `agent-smith/azurerepos/cloud-development/18693` |

Slug rules for the hierarchical form:

- Lower-cased, non-alphanumeric runs collapsed to `-`, leading/trailing `-` trimmed
- Slugs longer than 64 characters are truncated and suffixed with a 7-char SHA-1 hash of the original slug to keep the result deterministic
- A `projectName` that slugifies to empty (e.g. `"!!!---???"`) is rejected at compose time as a configuration error

Composition lives in `TicketBranchNamer` (`AgentSmith.Application.Services`) — a static helper. There is no DI registration; builders call it directly.

## The resume path

When `CheckoutSourceCommand` runs, it composes the work-branch name from the ticket and asks the source provider to check out that branch. The provider's `CheckoutBranch` does:

1. Look for a local branch with that name. If found, check it out — done.
2. Fetch from `origin`.
3. Look for `refs/remotes/origin/{branch}`. If found, create a local tracking branch from it and check it out — **resume**.
4. Otherwise, create a fresh branch from the current `HEAD` (legacy behavior — first attempt for this ticket).

This means: if a previous pipeline run pushed a `[wip]` commit, the next run picks up exactly where the prior run stopped. No replays of expensive analyzer calls; the agentic loop sees the prior tool output as committed file state.

## The persist path

`PipelineExecutor` wraps every pipeline run. When any step returns a failed `CommandResult`, the executor invokes `PersistWorkBranchHandler` **before** calling `lifecycle.MarkFailed()`. The handler runs in its own try/catch — a persist failure must never mask the original pipeline failure.

The handler:

1. Reads the `Repository` from the pipeline context. If absent (the pipeline failed before checkout), records `Unknown` and returns Fail.
2. Builds a `[wip] agent-smith run {runId}` commit message with three trailers (`Run-Id`, `Pipeline`, `Failed-Step`) so the commit is searchable from a log line.
3. Calls `ISourceProvider.CommitAndPushAsync`.
4. Classifies any thrown exception into a `PersistFailureKind` and stamps it onto `ContextKeys.PersistFailureKind` for the executor's logging wrapper to route on.

### Failure kinds

| Kind | Trigger | Operator action |
|------|---------|-----------------|
| `NoChanges` | Working tree was clean (provider returned an empty-commit signal) | Informational — the pipeline failed before producing any file changes; nothing to persist |
| `AuthDenied` | Push rejected with HTTP 401/403 or "unauthorized" | Check the source-provider PAT/credentials; the pipeline run still has output in logs |
| `RemoteDivergent` | Push rejected as `non-fast-forward` | Two pipeline runs raced on the same ticket; investigate the older branch on the remote and decide which to keep — the framework refuses to force-push |
| `NetworkBlip` | `HttpRequestException` during push | Transient — operator can re-trigger the ticket and the resume path will pick up the local state if the next run lands on the same pod, otherwise the work is lost |
| `Unknown` | Anything else | Inspect the log for the underlying exception |

Persist failures are logged at `Error` (or `Warning` for `NetworkBlip`) — they show up in the run telemetry next to the original pipeline failure, not in place of it.

## What this does not protect

- **Pipeline failure between commands within a single transactional step**: persistence happens at command boundaries. A handler that produces partial in-memory state without writing to disk is unaffected.
- **A successful run**: persistence runs only on the failure path. Successful runs commit and push as part of `CommitAndPRCommand` and do not need the WIP fallback.
- **First-attempt failures with no checkout**: if the pipeline fails before `CheckoutSourceCommand`, there is no working tree to persist (`Unknown` kind, no commit pushed).

## Related

- [Ticket Lifecycle](ticket-lifecycle.md) — where persist sits in the InProgress → Failed transition
- [Pipeline System](pipeline-system.md) — command/handler model and failure path
- [Cost Tracking](cost-tracking.md) — why preserving partial work matters for token budgets
