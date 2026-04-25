# Polling vs Webhooks

Both ingress paths feed the same `TicketClaimService` — once a claim succeeds, the pipeline runs identically. The choice is operational: latency, network topology, cost, and platform coverage.

## Quick Decision

| You have... | Use |
|-------------|-----|
| A reachable HTTPS endpoint and want minimum latency | Webhooks |
| Network egress only (private K8s, no inbound) | Polling |
| GitLab/AzDO/Jira and want to start today | Webhooks (polling is GitHub-only at runtime) |
| A flaky webhook delivery history and want self-healing | Both (coexistence is safe) |
| A cost-sensitive deployment with rare ticket activity | Webhooks (polling does API calls even when nothing happens) |

## Decision Matrix

| Dimension | Webhooks | Polling |
|-----------|----------|---------|
| **Latency from label to claim** | Sub-second (delivery time) | `interval_seconds ± jitter` (default 60s) |
| **Network requirement** | Public HTTPS endpoint reachable from the platform | Outbound only — Agent Smith calls the platform's REST API |
| **Setup per platform** | Webhook config in platform UI + secret in env | One YAML stanza, no platform-side changes |
| **API call cost (steady state)** | Zero unless events fire | One listing per cycle per project, plus per-ticket ClaimAsync |
| **API call cost (active)** | One per event | Same listing cost — events ride within the existing cycle |
| **Resilience to receiver pod restart** | Events delivered while pod down are lost (platform-dependent retry) | Next cycle catches up; no events to miss |
| **Resilience to platform delivery delays** | Subject to platform's webhook queue health | Independent — pulls from current state |
| **Multi-replica behaviour** | Every replica receives webhooks; whoever wins SETNX claims | Single leader polls; followers run consumer + housekeeping |
| **Authentication** | Webhook signature (HMAC for GitHub, token for GitLab, etc.) | Same auth as the ticket provider (PAT, API token) |
| **Platform coverage today** | All four (GitHub, GitLab, AzDO, Jira) | GitHub only |

## Common Scenarios

### "We run in private Kubernetes with no public ingress"

Polling is the only option. Set up:

```yaml
projects:
  internal-api:
    polling:
      enabled: true
      interval_seconds: 60
```

GitHub-only at runtime. For GitLab/AzDO/Jira projects in this scenario, polling support is the work item that unblocks them — see [polling.md](polling.md#current-platform-coverage).

### "We have webhooks but want a safety net"

Enable both. They coexist via the SETNX claim-lock — first claimer wins, second sees `AlreadyClaimed`. Polling at a relaxed interval (180–300s) acts as a periodic reconcile against missed webhook deliveries:

```yaml
projects:
  my-api:
    github_trigger:
      pipeline_from_label:
        agent-smith: fix-bug
    polling:
      enabled: true
      interval_seconds: 180
```

In normal operation: the webhook fires within seconds and the poller's next cycle finds nothing. When a webhook delivery fails: the poller picks up the orphaned Pending ticket within ~3 minutes.

### "We need <5 second latency"

Webhooks. Polling's floor is `interval_seconds` minus jitter (so ~54s with defaults). Setting `interval_seconds: 5` would meet the latency requirement but burns rate limit — and your platform may throttle you.

### "We're cost-sensitive and tickets are rare"

Webhooks. A project with one triggered ticket per week, polled every 60s, makes 10,080 listing calls per week to find one ticket. Webhooks make exactly one inbound delivery for that ticket. The platform's free tier likely covers both, but the math swings hard for high project counts.

### "We don't trust our webhook signature secret rotation"

Polling has a smaller secret blast radius — it uses your existing ticket-provider auth (`GITHUB_TOKEN` etc.) which already exists for the rest of Agent Smith. No webhook secret to manage, rotate, or leak.

## Hybrid: Webhook for Triggers, Poll for Recovery

A pattern that doesn't appear elsewhere in this comparison: use webhooks for normal triggering (low latency, cost-effective) and polling at a long interval purely for recovery against occasional webhook misses.

```yaml
polling:
  enabled: true
  interval_seconds: 300    # 5 min — pure safety net
  jitter_percent: 20
```

A 5-minute reconcile interval costs ~288 listings per project per day. For most teams that's negligible against API quota and vastly preferable to a Friday-evening missed webhook becoming a Monday-morning surprise.

## What Polling Does Not Do

- **No replay of missed events.** Polling looks at current ticket state, not event history. If a ticket was Pending → Enqueued → Done in five seconds and the polling interval is 60s, the poller sees Done and does nothing — which is correct.
- **No comment/PR triggers.** Polling drives the ticket-lifecycle path only. PR comments, dialogue answers, and other free-form triggers still need the webhook path. PR-comment webhooks are independent of the lifecycle and don't go through `TicketClaimService`.
- **No real-time ack.** A webhook responds with HTTP 202 (or 200) in milliseconds, giving the platform a delivery confirmation. Polling has no equivalent — there's no caller to acknowledge. That's a feature when the platform is unreachable, but a downside if your audit trail expects platform-side delivery records.

## Related

- [Polling Setup](polling.md) — config and operations
- [Webhook Configuration](../configuration/webhooks.md) — the alternative ingress
- [Ticket Lifecycle](../concepts/ticket-lifecycle.md) — what happens after a claim
- [Setup Guides](README.md) — overview of all ingress options
