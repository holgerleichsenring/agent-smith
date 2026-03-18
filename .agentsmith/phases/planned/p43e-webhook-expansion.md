# Phase 43e: Webhook Expansion — GitHub PR, GitLab MR, Azure DevOps

## Goal

Extend the existing `WebhookListener` to handle PR/MR label events from all
three platforms. When a `security-review` label is added, trigger the
`security-scan` pipeline. Also support Slack/Teams commands.

Depends on p43b (security pipeline) and p43c (SARIF output) being in place.

---

## Current State

`WebhookListener` (p14) in `AgentSmith.Host/Services/WebhookListener.cs`:
- Uses `System.Net.HttpListener` on port 8081
- Handles only GitHub Issues events (`X-GitHub-Event: issues`, action `labeled`)
- Hard-coded label `agent-smith`
- No signature verification
- No PR event support
- No GitLab or Azure DevOps support

---

## WebhookListener Refactor

Replace single monolithic handler with a dispatch pattern:

```csharp
// src/AgentSmith.Contracts/Services/IWebhookHandler.cs
public interface IWebhookHandler
{
    bool CanHandle(string platform, string eventType);
    Task<WebhookResult> HandleAsync(string payload, IDictionary<string, string> headers,
                                     CancellationToken ct);
}

public sealed record WebhookResult(bool Handled, string? TriggerInput, string? Pipeline);
```

`WebhookListener` becomes a thin HTTP server that:
1. Reads headers + body
2. Determines platform from headers (`X-GitHub-Event`, `X-Gitlab-Event`, etc.)
3. Dispatches to matching `IWebhookHandler` via `IEnumerable<IWebhookHandler>`
4. Enqueues result into Redis job queue

### Webhook Handlers

**`GitHubIssueWebhookHandler`** — existing behavior (label `agent-smith` on issue)

**`GitHubPrLabelWebhookHandler`** — NEW
- Event: `pull_request`, action: `labeled`, label: `security-review`
- Maps to `SecurityScanRequest`

**`GitLabMrLabelWebhookHandler`** — NEW
- Event: `Merge Request Hook`, label `security-review` added
- Maps to `SecurityScanRequest`

**`AzureDevOpsWorkItemWebhookHandler`** — NEW
- Event: `workitem.updated`, tag `security-review` added
- Maps to `SecurityScanRequest`

### SecurityScanRequest

```csharp
public sealed record SecurityScanRequest(
    string RepoUrl,
    string? PrIdentifier,
    string ProjectName,
    string Platform);
```

All handlers map their platform-specific payload to this common record.

---

## Webhook Signature Verification

Add per-platform signature verification:

- **GitHub**: `X-Hub-Signature-256` header, HMAC-SHA256 with webhook secret
- **GitLab**: `X-Gitlab-Token` header, compare with configured secret token
- **AzDO**: Basic auth or shared secret in payload

Secrets:
```yaml
webhooks:
  github_secret: ${GITHUB_WEBHOOK_SECRET}
  gitlab_token: ${GITLAB_WEBHOOK_TOKEN}
  azdo_secret: ${AZDO_WEBHOOK_SECRET}
```

---

## Slack / Teams Command

Extend existing intent engine in Dispatcher:

```
/security-review PR#42 in my-api
```

New intent pattern maps to `SecurityScanRequest` and enqueues into Redis.

- Slack: new slash command registration
- Teams: new adaptive card action

Reuses existing `IPlatformAdapter` infrastructure from Dispatcher.

---

## Config: Webhook Registration

```yaml
projects:
  my-api:
    security_scan: on_label
    webhooks:
      github: true
      gitlab: true
      azdo: false
```

`security_scan: never` → no webhook handler registered for that project.
`security_scan: on_label` → handler registered, triggers on label.
`security_scan: on_pr` → handler triggers on every PR open/update (no label needed).

---

## Files to Create

- `src/AgentSmith.Contracts/Services/IWebhookHandler.cs` — interface + WebhookResult
- `src/AgentSmith.Host/Services/Webhooks/GitHubPrLabelWebhookHandler.cs`
- `src/AgentSmith.Host/Services/Webhooks/GitLabMrLabelWebhookHandler.cs`
- `src/AgentSmith.Host/Services/Webhooks/AzureDevOpsWorkItemWebhookHandler.cs`
- `src/AgentSmith.Host/Services/Webhooks/GitHubIssueWebhookHandler.cs` — extracted from WebhookListener
- Tests: each handler with sample payloads, signature verification

## Files to Modify

- `src/AgentSmith.Host/Services/WebhookListener.cs` — refactor to dispatch pattern
- `src/AgentSmith.Dispatcher/` — Slack/Teams intent for `/security-review`

---

## Definition of Done

- [ ] `WebhookListener` refactored to `IWebhookHandler` dispatch pattern
- [ ] GitHub: PR label `security-review` triggers security-scan pipeline
- [ ] GitLab: MR label `security-review` triggers security-scan pipeline
- [ ] Azure DevOps: Work Item tag `security-review` triggers security-scan pipeline
- [ ] Existing GitHub Issues webhook still works (no regression)
- [ ] Webhook signature verification for all three platforms
- [ ] Slack: `/security-review PR#42 in my-api` triggers scan
- [ ] Teams: same command triggers scan
- [ ] `security_scan` config controls webhook registration (never/on_label/on_pr)
- [ ] Unit tests: each handler with real platform payloads (anonymized)
- [ ] Integration test: webhook → Redis enqueue → pipeline trigger
