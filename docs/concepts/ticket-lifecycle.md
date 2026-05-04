# Ticket Lifecycle

Every pipeline triggered from a ticket — webhook or poll — passes through the same lifecycle. The state lives on the ticket itself (as a label or tag), so an operator can inspect status without touching Redis or logs.

## States

| State | Label/Tag | Meaning |
|-------|-----------|---------|
| **Pending** | _no lifecycle label_ or `agent-smith:pending` | Ticket is eligible to be claimed. Default for any new triggered ticket. |
| **Enqueued** | `agent-smith:enqueued` | A receiver has claimed the ticket and pushed a `PipelineRequest` onto the Redis job queue. |
| **InProgress** | `agent-smith:in-progress` | A consumer pulled the request and a pipeline is running. Heartbeat is being renewed in Redis. |
| **Done** | `agent-smith:done` | Pipeline finished successfully. |
| **Failed** | `agent-smith:failed` | Pipeline failed. Error is posted as a comment on the ticket. |

The `agent-smith:` prefix marks lifecycle labels owned by the framework. Other labels are left untouched on every transition.

## State diagram

```
       (operator/poll/webhook)
                │
                ▼
            ┌────────┐
            │ Pending│
            └────┬───┘
                 │ TicketClaimService.ClaimAsync
                 │  (SETNX claim-lock + atomic transition)
                 ▼
            ┌────────┐
            │Enqueued│◄──── EnqueuedReconciler re-enqueues
            └────┬───┘      if queue lost the request
                 │ PipelineQueueConsumer dequeues
                 │  → PipelineExecutor begins
                 ▼
            ┌────────┐
            │InProgr │──── StaleJobDetector reverts to Pending
            └─┬────┬─┘     if no heartbeat for 2min
              │    │
   (success)  │    │  (failure)
              ▼    ▼
          ┌────┐ ┌──────┐
          │Done│ │Failed│
          └────┘ └──────┘
```

## Who owns each transition

| Transition | Owner | When |
|------------|-------|------|
| Pending → Enqueued | `TicketClaimService` (Server) | Webhook or poll fires; pre-checks pass; SETNX claim-lock acquired |
| Enqueued → InProgress | `PipelineExecutor` (**inside the spawned CLI container**, p0113) | Container loads `PipelineRequest` from `IPipelineRequestStore` and starts the pipeline |
| InProgress → Done | `PipelineExecutor` (container, via `LifecycleScope.Dispose`) | All pipeline commands succeeded |
| InProgress → Failed | `PipelineExecutor` (container, via `LifecycleScope.MarkFailed` + Dispose) | Any command failed; error comment posted to ticket |
| InProgress → Pending | `StaleJobDetector` | No Redis heartbeat for the ticket and the container is gone — pipeline is presumed crashed |
| Enqueued → Pending | `EnqueuedReconciler` | Ticket is Enqueued but no container ever wrote a heartbeat — spawn failed or container crashed before lifecycle init (see [Spawned Jobs](spawned-jobs.md)) |

## Concurrency primitives

Three Redis primitives carry the contract:

| Key pattern | Purpose | TTL |
|-------------|---------|-----|
| `agentsmith:claim-lock:{platform}:{ticketId}` | SETNX mutex around the read-current-status → transition window. Prevents webhook + poll racing on the same ticket. | 30s |
| `agentsmith:heartbeat:{ticketId}` | Liveness signal from the running pipeline. Renewed every 30s. Absence after TTL means the pipeline is presumed dead. | 2min |
| `agentsmith:queue:jobs` | The job queue itself (Redis list, RPUSH/LPOP FIFO). | none — list is the queue |

For Jira's label mode (no atomic label PATCH), an additional `agentsmith:jira-label-lock:{ticketId}` (10s TTL) wraps the actual PATCH inside the global claim-lock window.

For the poller and housekeeping coordinator, two leader leases:

| Key | Holder responsibility |
|-----|----------------------|
| `agentsmith:leader:poller` | Runs the `PollerHostedService` for all configured pollers. Single replica polls. |
| `agentsmith:leader:housekeeping` | Runs `StaleJobDetector` and `EnqueuedReconciler`. Single replica reconciles. |

A single-replica deployment holds both leases. Leases use SETNX with CAS-checked renewal/release, so a GC-paused or partitioned holder cannot affect the new holder after TTL expiry.

## Atomic transitions per platform

The lifecycle is the source of truth, so transitions must be atomic against concurrent modifications. Each platform uses the strongest primitive its API offers:

| Platform | Mechanism | Failure mode |
|----------|-----------|--------------|
| **GitHub** | GET captures ETag; PATCH `/issues/{n}` with `If-Match: {ETag}` and a full labels array | 412 Precondition Failed → `PreconditionFailed` outcome (race with another writer) |
| **Azure DevOps** | JSON Patch with explicit `test /rev` operation before the `add /fields/System.Tags` | 412/409 → `PreconditionFailed` (rev mismatch) |
| **GitLab** | `PUT /issues/{iid}` with `add_labels`/`remove_labels` (targets only lifecycle labels, leaves others untouched) | No ETag — last-write-wins at the platform; SETNX claim-lock is the primary race guard |
| **Jira (label mode)** | `PUT /rest/api/3/issue/{key}` with labels update operations, wrapped in an additional SETNX lock | Lock held → `PreconditionFailed`; otherwise success |

## What survives what

| Failure | What's lost | What recovers it |
|---------|-------------|------------------|
| Receiver pod crash mid-claim | Nothing — claim is atomic per ticket | Next webhook/poll re-claims; the lock TTL is 30s |
| Consumer pod crash mid-pipeline | Heartbeat stops renewing; ticket is left InProgress | StaleJobDetector reverts to Pending within 1 minute after TTL |
| Redis loss + restart | Queue is empty, all heartbeats gone | EnqueuedReconciler re-enqueues every Enqueued ticket within 10 minutes; StaleJobDetector reverts orphaned InProgress within ~3 minutes |
| Network partition between receiver and consumer pods | Nothing — receiver only enqueues, consumer pulls when reachable | Self-heals once partition lifts |
| Operator manually deletes lifecycle label | Ticket appears Pending again | Next webhook/poll re-claims it |

## Observability

Every state transition logs at Information level with `ticketId`, `platform`, `outcome`. The reconciler logs every recovery action ("re-enqueued orphan ticket X"). For dashboards, OpenTelemetry counters land in a follow-up phase.

To inspect lifecycle state for a project at a glance, list issues by label in the platform's UI:

- GitHub: `is:issue label:agent-smith:in-progress`
- GitLab: filter by label `agent-smith:in-progress`
- Azure DevOps: WIQL `[System.Tags] CONTAINS 'agent-smith:in-progress'`
- Jira: `labels = "agent-smith:in-progress"`

## Related

- [Spawned Jobs](spawned-jobs.md) — how the cross-process boundary is wired
- [Polling Setup](../setup/polling.md) — opt-in per project
- [Webhook Configuration](../configuration/webhooks.md) — claim flow and HTTP responses
- [Polling vs Webhooks](../setup/polling-vs-webhooks.md) — when to choose which
- [Architecture Layers](../architecture/layers.md) — where the claim flow sits
