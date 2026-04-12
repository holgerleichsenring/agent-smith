# Phase 72: Jira Assignee Webhook Trigger

## Goal

When a Jira issue is assigned to "Agent Smith", Agent Smith automatically
starts the appropriate pipeline for that ticket. The pipeline is determined
by the issue's Jira labels — with a configurable default as fallback.

---

## Prerequisites

- p16 completed (JiraTicketProvider, REST v3)
- p43e completed (IWebhookHandler dispatch pattern, WebhookListener refactored)

---

## How It Works

```
User assigns Jira issue to "Agent Smith"
        ↓
Jira fires webhook: jira:issue_updated (assignee changed)
        ↓
WebhookListener → JiraAssigneeWebhookHandler.CanHandle()
        ↓
Parse payload: issue key + labels
        ↓
Resolve pipeline: label-map → default_pipeline
        ↓
JobEnqueuer.EnqueueAsync(JobRequest)
        ↓
Agent Smith starts pipeline
```

---

## Config

Extend `agentsmith.yml` — new `jira_trigger` block inside a project:

```yaml
projects:
  my-project:
    ticket:
      type: jira
      url: https://mycompany.atlassian.net

    jira_trigger:
      assignee_name: "Agent Smith"        # Jira display name of the Agent Smith user
      secret: ${JIRA_WEBHOOK_SECRET}      # optional shared secret for verification
      pipeline_from_label:
        security-review: security-scan
        mad-discussion:  mad-discussion
        legal:           legal-analysis
      default_pipeline: fix-bug           # fallback when no label matches
```

`assignee_name` must match the Jira display name exactly (case-insensitive comparison
in code). `secret` is optional — when omitted, signature verification is skipped
(acceptable for internal networks; recommended for internet-facing deployments).

### Config Model

```
File: src/AgentSmith.Contracts/Configuration/JiraTriggerConfig.cs
```

```csharp
public sealed class JiraTriggerConfig
{
    public string AssigneeName { get; set; } = "Agent Smith";
    public string? Secret { get; set; }
    public Dictionary<string, string> PipelineFromLabel { get; set; } = new();
    public string DefaultPipeline { get; set; } = "fix-bug";
}
```

Add to `ProjectConfig`:

```csharp
public JiraTriggerConfig? JiraTrigger { get; set; }
```

---

## Jira Webhook Payload

Jira Cloud sends this when an issue is updated (assignee changed):

```json
{
  "webhookEvent": "jira:issue_updated",
  "issue_event_type_name": "issue_assigned",
  "issue": {
    "key": "PROJ-123",
    "fields": {
      "assignee": {
        "displayName": "Agent Smith",
        "emailAddress": "agentsmith@mycompany.com"
      },
      "labels": ["security-review", "backend"]
    }
  },
  "changelog": {
    "items": [
      {
        "field": "assignee",
        "fieldtype": "jira",
        "fromString": "Alice Example",
        "toString": "Agent Smith"
      }
    ]
  }
}
```

Key paths:
- `changelog.items[].field == "assignee"` — confirms this is an assignee change
- `changelog.items[].toString` — the new assignee display name
- `issue.key` — ticket identifier (e.g. `PROJ-123`)
- `issue.fields.labels[]` — for pipeline resolution

Jira does not send a dedicated header for the event type (unlike GitHub's
`X-GitHub-Event`). The platform is identified by the absence of GitHub/GitLab
headers combined with the payload structure. Use a dedicated endpoint
`POST /webhook/jira` to avoid ambiguity, or detect via payload shape.

---

## Step 1: Config Model

**File:** `src/AgentSmith.Contracts/Configuration/JiraTriggerConfig.cs`

Add `JiraTriggerConfig` record as shown above.

**File:** `src/AgentSmith.Contracts/Configuration/ProjectConfig.cs`

Add:
```csharp
public JiraTriggerConfig? JiraTrigger { get; set; }
```

**File:** `config/agentsmith.yml` (example config)

Add the `jira_trigger` block to the Jira project example.

---

## Step 2: JiraAssigneeWebhookHandler

**File:** `src/AgentSmith.Cli/Services/Webhooks/JiraAssigneeWebhookHandler.cs`

> **Implementation diverged from plan** — see "Implementation Notes" at bottom.
> Handler returns `WebhookResult(Handled, TriggerInput, Pipeline)` following
> the existing dispatch pattern. Config access via `ServerContext(ConfigPath)`
> injected through DI. Signature validation moved to `WebhookSignatureValidator`
> and executed by the `WebhookListener` before dispatch (like all other platforms).

---

## Step 3: WebhookListener — Platform Detection

The `WebhookListener` detects the platform from headers. Jira has no
dedicated event-type header, so add a dedicated route or detect by absence:

**Option A (recommended): Dedicated endpoint**

Register `POST /webhook/jira` in `WebhookListener` alongside the existing
`POST /webhook`. Both dispatch via `IWebhookHandler`, but the platform string
is set explicitly:

```csharp
// In WebhookListener request handling:
var platform = context.Request.Url?.AbsolutePath switch
{
    "/webhook/jira"   => "jira",
    "/webhook/github" => DetermineGitHubPlatform(headers),
    "/webhook/gitlab" => "gitlab",
    "/webhook"        => DetermineGenericPlatform(headers),  // legacy
    _                 => null
};
```

**Jira event type extraction:** Jira has no event-type header. For the
`/webhook/jira` route the `WebhookListener` reads `webhookEvent` from the
JSON payload root (e.g. `"jira:issue_updated"`) and strips the `"jira:"`
prefix to produce the `eventType` string passed to `CanHandle`. This is a
one-time parse in the Listener before dispatching — handlers never touch
the raw event type field.

**Option B: Payload-shape detection (fallback)**

If a single `/webhook` endpoint is preferred, detect Jira by checking for
`webhookEvent` in the root JSON object — GitHub/GitLab never use that field.

---

## Step 4: DI Registration

**File:** `src/AgentSmith.Cli/ServiceProviderFactory.cs`

```csharp
services.AddSingleton<IWebhookHandler, Services.Webhooks.JiraAssigneeWebhookHandler>();
```

`ServiceProviderFactory.Build()` gains an optional `configPath` parameter.
When provided (server mode), it registers `ServerContext(configPath)` as a
singleton — this is how the Jira handler gets access to config without
changing the `IWebhookHandler` interface.

**File:** `src/AgentSmith.Cli/Commands/ServerCommand.cs`

Passes `configPath` to the factory: `ServiceProviderFactory.Build(..., configPath)`.

---

## Step 5: Jira Webhook Setup (Ops)

One-time setup in the Jira Cloud admin console:

**Settings → System → WebHooks → Create a WebHook**

| Field | Value |
|---|---|
| Name | `Agent Smith Trigger` |
| URL | `https://your-agentsmith.example.com/webhook/jira` |
| Secret | value of `${JIRA_WEBHOOK_SECRET}` (optional) |
| Events | ✅ Issue → updated |
| JQL Filter | `assignee = "agentsmith@mycompany.com"` (optional, reduces noise) |

The JQL filter is optional but recommended — it limits webhook calls to only
issues assigned to Agent Smith, reducing unnecessary traffic.

---

## Out of Scope

- **Job cancellation on unassign:** When Agent Smith is removed as assignee,
  the webhook fires again (`assignee` change, `toString` ≠ Agent Smith).
  The handler correctly returns `Ignored`. Cancelling a running job for that
  ticket requires a Job Cancellation feature that does not exist yet — track
  separately.

---

## Files Created

- `src/AgentSmith.Contracts/Models/Configuration/JiraTriggerConfig.cs`
- `src/AgentSmith.Contracts/Models/Configuration/ServerContext.cs`
- `src/AgentSmith.Cli/Services/Webhooks/JiraAssigneeWebhookHandler.cs`
- `tests/AgentSmith.Tests/Webhooks/JiraAssigneeWebhookHandlerTests.cs`

## Files Modified

- `src/AgentSmith.Contracts/Models/Configuration/ProjectConfig.cs` — add `JiraTrigger`
- `src/AgentSmith.Cli/Services/WebhookListener.cs` — add `/webhook/jira` route, Jira event extraction, Jira signature validation
- `src/AgentSmith.Cli/Services/Webhooks/WebhookSignatureValidator.cs` — add `ValidateJira()`
- `src/AgentSmith.Cli/ServiceProviderFactory.cs` — register handler + optional `ServerContext`
- `src/AgentSmith.Cli/Commands/ServerCommand.cs` — pass `configPath` to factory
- `config/agentsmith.yml` — add example `jira_trigger` block
- `.agentsmith/context.yaml` — update Jira integration from `inbound` to `bidirectional`

---

## Tests

**File:** `tests/AgentSmith.Tests/Webhooks/JiraAssigneeWebhookHandlerTests.cs`

| Test | Scenario | Expected |
|---|---|---|
| `HandleAsync_AssigneeMatchesConfig_EnqueuesJob` | Valid payload, assignee = "Agent Smith" | `WebhookResult.Handled`, job enqueued |
| `HandleAsync_AssigneeDoesNotMatch_ReturnsIgnored` | Assignee = "Alice" | `WebhookResult.Ignored` |
| `HandleAsync_NotAnAssigneeChange_ReturnsIgnored` | Changelog has `status` change, not `assignee` | `WebhookResult.Ignored` |
| `HandleAsync_LabelMatchesPipelineMap_UsesMappedPipeline` | Label = `security-review` | Pipeline = `security-scan` |
| `HandleAsync_NoLabelMatch_UsesDefaultPipeline` | Labels = `["backend"]`, no map match | Pipeline = `fix-bug` |
| `HandleAsync_NoLabels_UsesDefaultPipeline` | `fields.labels` is empty array | Pipeline = `fix-bug` |
| `HandleAsync_MultipleLabels_ConfigOrderWins` | Labels = `["mad-discussion", "security-review"]`, config order: `security-review` first | Pipeline = `security-scan` |
| `HandleAsync_SecretMissing_SkipsVerification` | No secret in config | Returns `Handled` |
| `HandleAsync_SecretConfigured_HeaderMissing_ReturnsUnauthorized` | Secret in config, no `x-hub-signature` header | `WebhookResult.Unauthorized` |

Use a real `JsonDocument` with anonymized sample payloads — no need to mock JSON parsing.

---

## Definition of Done

- [ ] `JiraTriggerConfig` added to `ProjectConfig`
- [ ] `JiraAssigneeWebhookHandler` implements `IWebhookHandler`
- [ ] Assignee name matching is case-insensitive
- [ ] Pipeline resolved via label map, falls back to `default_pipeline`
- [ ] Secret verification skipped when `secret` not configured
- [ ] `/webhook/jira` route added to `WebhookListener`
- [ ] Handler registered in DI
- [ ] `agentsmith.yml` example includes `jira_trigger` block
- [ ] `context.yaml` updated: Jira → `bidirectional`
- [x] All 9 tests pass
- [x] `dotnet build` + `dotnet test` clean (873 tests, 0 failures)

---

## Implementation Notes (deviations from plan)

1. **No IJobEnqueuer / JobRequest** — the codebase uses `WebhookResult(Handled, TriggerInput, Pipeline)`.
   Handlers return data; `WebhookListener` calls `ExecutePipelineUseCase`. The plan's
   `IJobEnqueuer.EnqueueAsync` pattern does not exist in the codebase.

2. **ServerContext for config path** — the plan assumed `IConfigurationLoader.LoadAsync(ct)` (async, no
   path parameter). Actual interface is `LoadConfig(string configPath)` (sync). Introduced
   `ServerContext(ConfigPath)` record registered at server startup to bridge the gap.

3. **Signature validation in WebhookListener, not handler** — all other platforms validate signatures
   in the Listener before dispatch. Jira follows the same pattern via `ValidateJiraSignature()` in
   the Listener + `WebhookSignatureValidator.ValidateJira()`. The handler does not touch signatures.

4. **Platform-specific routes** — added `/webhook/github`, `/webhook/gitlab`, `/webhook/jira` alongside
   the legacy `/webhook`. All routes dispatch through the same handler chain. Jira route uses
   `ExtractJiraEventType()` to parse `webhookEvent` from payload body.
