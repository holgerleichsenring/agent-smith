# Polling Setup

Polling is an alternative ingress path to webhooks. Instead of the platform pushing events to Agent Smith, Agent Smith pulls eligible tickets on an interval. Both paths feed the same `TicketClaimService` — downstream behaviour is identical.

Use polling when your Agent Smith deployment cannot accept inbound HTTP from the platform (private Kubernetes, no public ingress, restrictive firewall). Use webhooks when you need low-latency triggering and have a reachable endpoint.

For the deeper comparison: [Polling vs Webhooks](polling-vs-webhooks.md).

## Prerequisites

- Agent Smith running in server mode (`agent-smith server --port 8081`).
- Redis reachable via `REDIS_URL` env var. Polling needs the same Redis as the claim/queue infrastructure.
- The configured ticket auth (e.g. `GITHUB_TOKEN`) has read access to the project AND write access to issue labels — pollers and the lifecycle transitioner both write `agent-smith:*` labels.

## Current Platform Coverage

All four platforms support polling. Listing is platform-native — each provider implements `ITicketProvider.ListByLifecycleStatusAsync(Pending)` against its own API. One listing call per project per cycle.

| Platform | Listing API | Status |
|----------|-------------|--------|
| GitHub | `GET /repos/{owner}/{repo}/issues?labels=agent-smith:pending&state=all` | Supported |
| GitLab | `GET /projects/{id}/issues?labels=agent-smith:pending&state=opened` | Supported |
| Azure DevOps | WIQL `[System.Tags] CONTAINS 'agent-smith:pending' AND [System.State] IN (...openStates...)` | Supported |
| Jira | JQL `project = "{key}" AND labels = "agent-smith:pending"` (POST /rest/api/3/search) | Supported (label-mode) |

### Required token scopes

| Platform | Scope / permission |
|----------|---------------------|
| GitHub | Token with `repo` (read access to issues + write access to labels) |
| GitLab | Personal access token with `api` scope |
| Azure DevOps | PAT with **Work Items: Read & Write** |
| Jira | API token + email; user must have **Browse Projects** + **Edit Issues** on the project |

### Platform-specific notes

- **Azure DevOps**: WIQL also filters by the project's configured `open_states` (default `New`/`Active`/`Committed`). A Pending-tagged work item already in `Closed` is not picked up.
- **Jira**: Label-mode only in the current implementation. If `tickets.project` is set in `agentsmith.yml`, the JQL is scoped to that project key; otherwise the search is instance-wide and matches any issue with the lifecycle label. Native-status-mode polling (probing `JiraWorkflowCatalog` for transitions) is deferred.
- **GitLab**: Listing returns at most 100 issues per cycle (per-page max). Backlog drains naturally over multiple cycles.

## Minimal Configuration

```yaml
projects:
  my-api:
    source:
      type: GitHub
      url: https://github.com/org/my-api
    tickets:
      type: GitHub
      url: https://github.com/org/my-api
      auth: token
    pipeline: fix-bug

    polling:
      enabled: true            # default: false
```

That's the minimum. Defaults are sensible:

| Key | Default | Description |
|-----|---------|-------------|
| `enabled` | `false` | Whether to poll this project |
| `interval_seconds` | `60` | Base sleep between poll cycles |
| `jitter_percent` | `10` | Random ±% applied to the interval |

## Full Configuration

```yaml
projects:
  my-api:
    # ... source, tickets, agent ...

    pipeline: fix-bug

    # Trigger config decides which pipeline a polled ticket runs.
    # Polling and webhook share this section — pipeline_from_label
    # maps Pending-labelled tickets to pipelines (default_pipeline
    # is the fallback). For polling without webhooks, only
    # default_pipeline matters; pipeline_from_label only applies
    # when the trigger label is added by a human.
    github_trigger:
      default_pipeline: fix-bug
      pipeline_from_label:
        agent-smith: fix-bug
        security-review: security-scan

    polling:
      enabled: true
      interval_seconds: 30
      jitter_percent: 15

agent:
  queue:
    max_parallel_jobs: 4       # consumer-side backpressure
    consume_block_seconds: 5
    shutdown_grace_seconds: 30
```

## Coexistence with Webhooks

Both paths can be active for the same project. Whichever fires first wins the SETNX claim-lock; the second sees the ticket already in `Enqueued` status and returns `AlreadyClaimed` cleanly. No duplicate pipeline runs.

Use this for redundancy: webhook for low-latency in normal operation, polling as a safety net during webhook outages or platform delivery delays.

## How Polling Runs

A single replica per process holds the `agentsmith:leader:poller` Redis lease (30s TTL, renewed every 10s). The leader runs `PollerHostedService`, which:

1. Loops over every project with `polling.enabled: true`.
2. Calls each platform poller's `PollAsync` in parallel via `Task.WhenAll` with a 20s per-poller timeout.
3. For every returned `ClaimRequest`, calls `TicketClaimService.ClaimAsync` sequentially.
4. Sleeps for `min(interval_seconds across pollers) ± jitter`, then loops.

If the leader pod crashes, another pod acquires the lease within ~30s. Followers don't poll but still process queue items as workers (the consumer is per-pod, not leader-only).

## Operator Tasks

### Bootstrap a project for polling

1. Add `polling: { enabled: true }` to the project in `agentsmith.yml`.
2. Restart the Agent Smith server (or wait — config is reloaded each cycle, but a restart guarantees clean DI registration of the poller).
3. On the next cycle, eligible Pending-labelled tickets begin claiming.

### Add a Pending-labelled ticket without a webhook

Add the configured trigger label (e.g. `agent-smith`) to a GitHub issue. The poller picks it up on its next cycle. Lifecycle proceeds: `Pending → Enqueued → InProgress → Done/Failed`.

### Reset a stuck ticket

If a ticket is stuck in `agent-smith:in-progress` and you've confirmed no pipeline is actually running:

- Wait up to 1 minute — `StaleJobDetector` reverts InProgress without a heartbeat to Pending automatically.
- Or manually: remove the `agent-smith:in-progress` label. The next claim attempt treats it as Pending.

### Disable polling for a project

Set `polling.enabled: false` (or remove the section). Pollers re-register on the next config load.

## Troubleshooting

### Stuck leader

Symptom: no poll cycles for >1 minute even though config has `enabled: true`.

Check: `redis-cli GET agentsmith:leader:poller` returns a non-empty token but no replica is logging poll cycles. The likely cause is a leader pod that died without releasing — wait for the 30s TTL, then verify a new pod acquires.

### Rate-limit breaches

Symptom: 403 / 429 from the platform.

Mitigation: increase `interval_seconds` (60 → 120 or 180) and ensure `jitter_percent` is non-zero. The poll uses one listing request per cycle per project, plus one ClaimAsync (which itself reads the ticket once before transitioning), so cycle cost scales with project count, not ticket count.

### Orphaned Enqueued tickets

Symptom: tickets are stuck in `agent-smith:enqueued` but never run.

Check: is a pod with `IRedisJobQueue` consumer running? `agent-smith server` starts both the consumer and the poller leader; without it, queue items just accumulate. `EnqueuedReconciler` will eventually re-push within 10 minutes, but the consumer must run to drain the queue.

### Polling enabled but no claims

Symptom: `polling.enabled: true`, ticket has the trigger label, but nothing happens.

Checks (in order):

- The trigger label is exactly `agent-smith:pending` (not `agent-smith` alone).
- The token has the listing scope from the table above.
- For Jira, `tickets.project` is set if you have multiple projects on the same instance — otherwise the JQL search may match issues you didn't expect.
- For Azure DevOps, the work item is in one of `tickets.open_states` (default `New`/`Active`/`Committed`).
- Check the leader log: `agentsmith:leader:poller` is held by exactly one pod, and it logs poll cycles.

## Related

- [Polling vs Webhooks](polling-vs-webhooks.md) — decision matrix
- [Ticket Lifecycle](../concepts/ticket-lifecycle.md) — what happens after a claim
- [Webhook Configuration](../configuration/webhooks.md) — the alternative ingress
- [agentsmith.yml Reference](../configuration/agentsmith-yml.md) — full config schema
