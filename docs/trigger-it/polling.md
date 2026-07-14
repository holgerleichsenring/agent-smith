# Trigger: polling

The fallback when webhooks aren't an option. Agent Smith asks the tracker every N seconds what's new.

Use polling when:

- Your tracker is on a private network the orchestrator can't be reached from.
- You don't want to manage a public ingress for the orchestrator.
- You're iterating on the config and want a tight feedback loop without re-pointing webhooks.

Don't use polling when webhooks work — they're faster, cheaper on tracker rate limits, and don't have a per-poll delay.

## Config

```yaml
trackers:
  acme-platform:
    type: azure_devops
    # ... organization, project, auth ...
    polling:
      enabled: true
      interval_seconds: 60       # how often to ask
      jitter_percent: 10         # randomize ±10% to avoid thundering herds
```

Same shape for every tracker family (Azure DevOps, Jira, GitHub Issues, GitLab Issues). The `polling` block lives on the tracker, not on the project — every project sharing this tracker shares the poll.

## What gets polled

Per poll, the framework asks the tracker for tickets in `open_states` that have been updated since the last poll. When the tracker has trigger labels configured (`pipeline_from_label`), discovery narrows the server-side query to tickets carrying one of those concrete labels — on a shared tracker (the realistic case: your whole team's board) untagged tickets never even reach the matching step. A tracker triggered by status/area-path alone receives everything in its trigger statuses. Either way, a ticket claimed by one run is never picked up for a second. The poller reacts to:

- New tickets that match the project resolution rules and have a `pipeline_from_label`.
- Status transitions that put a ticket into a `trigger_statuses` value.
- Label adds (a `pipeline_from_label` label appears).

The claim itself is a database lease, shared with the webhook path — webhook and poll racing on the same ticket resolve to one run.

## Polling vs webhooks

| | Webhooks | Polling |
|---|---|---|
| Latency | Sub-second | Up to `interval_seconds` |
| Tracker network reachability | Tracker must reach orchestrator | Orchestrator must reach tracker |
| Tracker API rate limits | One request per ticket event | One request per `interval_seconds` |
| Secret to verify | Yes (HMAC or basic auth) | Auth token only |
| Survives orchestrator restarts | Tracker retries until it gets 200 | Yes (high-water mark in Redis) |

For most setups, **webhooks for the public trackers (GitHub Cloud, GitLab Cloud, Jira Cloud, Azure DevOps Cloud), polling for self-hosted ones behind a firewall**.

## Tuning the interval

`60` seconds is a good default. Faster (10–30 seconds) for development. Slower (300+ seconds) for trackers with aggressive rate limits or when you're paying per API call.

The `jitter_percent` field randomizes the actual interval by ±N% so multiple orchestrator replicas don't all hit the tracker at the same wall-clock moment. Default 10%. Don't set it to 0 unless you have exactly one replica.

## Leader lease

If you run more than one orchestrator replica, only one polls. The polling lease is a Redis key `agentsmith:leader:poller` with a TTL; one replica wins, the others wait. If the leader dies, the lease expires and another replica takes over within 30 seconds.

You don't need to configure this — it's automatic. Same model is used for the housekeeping coordinator (stale-job detection, enqueue reconciliation) on a separate lease.

## Mixed mode

You can have webhooks on one tracker and polling on another in the same config. Polling state is per-tracker, so they don't interfere.

```yaml
trackers:
  acme-github:                       # webhooks (secret lives in GITHUB_WEBHOOK_SECRET on the server)
    type: github
    url: https://github.com/acme-org/todolist
    auth: github_token
  acme-jira-onprem:                  # polling
    type: jira
    url: https://jira.acme-internal.example
    auth: jira_token
    polling: { enabled: true, interval_seconds: 90 }
```

## Next

- [Webhooks](webhooks.md) — if you can make webhooks work, do.
- [Labels](labels.md) — which labels trigger which pipeline.
- [Host it: kubernetes](../host-it/kubernetes.md) — multi-replica setup where the leader lease matters.
