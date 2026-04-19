# Azure DevOps Webhook Setup

Connect Azure DevOps to Agent Smith to trigger pipelines from work item tags and PR comments.

## Prerequisites

- Agent Smith running in server mode (`agent-smith server --port 8081`)
- Public URL reachable from Azure DevOps (e.g. via ngrok: `ngrok http 8081`)
- Azure DevOps project with Project Administrator access

## Supported Events

| Event | Handler | What it does |
|-------|---------|--------------|
| `workitem.updated` | AzureDevOpsWorkItemWebhookHandler | Triggers `security-scan` pipeline when `security-review` tag is present |
| PR comment | AzureDevOpsPrCommentWebhookHandler | PR comment commands: `/agent-smith fix-bug`, `/approve`, `/reject` |

## Step-by-Step Setup

### 1. Create Service Hook

1. Go to **Project Settings > Service hooks > Create subscription**
2. Select **Web Hooks** as the service
3. **Trigger:** Work item updated
4. **Filters:** Optionally filter by area path or work item type
5. **Action URL:** `https://your-host/webhook`
6. **HTTP headers:** Add `Authorization: Basic <base64-encoded-secret>`
7. Click **Finish**

For PR comments, create a second subscription:

1. **Trigger:** Pull request commented on
2. Same URL and authorization header

### 2. Configure Agent Smith

Set the webhook secret as environment variable:

```bash
export AZDO_WEBHOOK_SECRET="your-secret-here"
```

The secret is the raw value before Base64 encoding. Agent Smith decodes the `Authorization: Basic` header and compares.

### 3. Verify

1. Add the `security-review` tag to a work item
2. Check Agent Smith logs for: `Azure DevOps work item #N tagged for security review`
3. Or comment `/agent-smith fix-bug` on a pull request

## Signature Verification

Azure DevOps uses Basic authentication in the `Authorization` header. Agent Smith validates this against the `AZDO_WEBHOOK_SECRET` environment variable.

If no secret is configured, verification is skipped (development only).

!!! note "Tag-to-pipeline mapping"
    Currently, the Azure DevOps handler only triggers `security-scan` for the `security-review` tag. Configurable tag-to-pipeline mapping is planned for p84.

## Ticket Provider Configuration

The Azure DevOps ticket provider supports additional configuration:

```yaml
tickets:
  type: AzureDevOps
  organization: my-org
  project: my-project
  auth: token
  open_states: ["New", "Active"]                # States considered "open" (default: New, Active, Committed)
  done_status: "Resolved"                       # Target state when closing (default: Closed)
  extra_fields:                                 # Additional fields to fetch from work items
    - "Microsoft.VSTS.Common.Priority"
    - "Custom.MyField"
```

!!! tip "Process template compatibility"
    The `open_states` whitelist replaces the previous hardcoded state exclusions. Set this to match your Azure DevOps process template (Agile, Scrum, CMMI, or custom). Missing `extra_fields` map to null — they never cause errors.

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `AZDO_WEBHOOK_SECRET` | — | Raw secret for Basic auth verification |
| `AZDO_API_VERSION` | `7.1` | Azure DevOps REST API version for PR comment replies |

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Webhook returns 401 | Check `AZDO_WEBHOOK_SECRET` matches the value used in the Basic auth header |
| Work item event ignored | Verify the tag `security-review` is present in `System.Tags` (case-insensitive) |
| No events received | Ensure the service hook subscription is active and the URL is reachable |
