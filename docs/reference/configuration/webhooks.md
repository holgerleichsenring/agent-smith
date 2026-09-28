# Webhook Configuration

!!! note "Which surface reads this"
    The YAML on this page is the file format. On a server the same values live in the database and are edited in the [Config studio](../../configure-it/config-studio.md); the CLI reads them from `agentsmith.yml`. `agent-smith config import` moves one into the other. See [Where configuration lives](../../configure-it/index.md).

Agent Smith receives platform events via webhooks. Two distinct flows go through the receiver:

- **Ticket triggers** (issue or work item labelled, moved, assigned): these go through project resolution and the claim, and follow the [ticket lifecycle](../concepts/ticket-lifecycle.md). All four platforms.
- **Pull request events and PR comment commands**: GitHub, GitLab and Azure DevOps.

Polling is the alternative ingress for ticket triggers. See [Polling](../../trigger-it/polling.md).

## Ticket trigger flow

When a webhook arrives for a ticket event:

```
Platform webhook
      │ POST /webhook
      ▼
WebhookSignatureVerifier  (401 on a missing or bad signature, wherever a secret is configured)
      │
      ▼
Platform-specific handler  (GitHubIssueWebhookHandler, JiraAssigneeWebhookHandler, ...)
      │ builds an IncomingTicketEnvelope
      ▼
ProjectResolver            (which projects match, which pipeline each runs)
      │
      ▼
WebhookSpawnDispatcher → SpawnPipelineRunsUseCase
      │ footprint check → queue or claim → enqueue
      ▼
HTTP response to platform
```

The HTTP response says what the receiver did with the delivery, not how the run went:

| Status | Body | Meaning |
|:------:|------|---------|
| 202 | `Accepted` | a handler took the event (a run was claimed or queued, or a command started) |
| 200 | `Event ignored` | no handler wanted this event, or it matched nothing |
| 200 | `Unknown platform` | the platform couldn't be detected |
| 401 | `Signature validation failed` | a secret is configured and the delivery didn't prove it |

## Supported Platforms

| Platform | Trigger events | PR comment commands | Signature method |
|----------|----------------|:-------------------:|------------------|
| GitHub | `issues` (labeled) | Yes | HMAC-SHA256 (`X-Hub-Signature-256`) |
| GitLab | `Issue Hook` (labeled) | Yes | Token header (`X-Gitlab-Token`) |
| Azure DevOps | `workitem.updated` | Yes | Basic auth |
| Jira | `issue_updated` (assigned), `comment_created` | No | HMAC (`x-hub-signature`) |

## Webhook Secrets

Webhook secrets are **environment variables on the server process** — there is no `webhooks:` block in `agentsmith.yml`:

| Env var | Platform | Verification |
|---------|----------|--------------|
| `GITHUB_WEBHOOK_SECRET` | GitHub | HMAC (`X-Hub-Signature-256`) |
| `GITLAB_WEBHOOK_TOKEN` | GitLab | Token compare (`X-Gitlab-Token`) |
| `AZDO_WEBHOOK_SECRET` | Azure DevOps | Basic auth |

Jira is the exception: its secret lives in config, per project, under
`projects.<name>.jira_trigger.secret`. Written as `${NAME}` it resolves against the
`secrets:` map first and then against the environment variable of that name; a reference
that resolves to nothing counts as **unconfigured**, not as a secret. Several projects
may each carry a different Jira secret — a delivery is accepted when any one of them
verifies it.

!!! warning "A configured secret is enforced"
    Once a platform has a secret, a delivery that arrives **without** a valid signature is
    refused with `401` — an absent signature header included. A platform with **no** secret
    configured is not verified at all and every delivery is accepted; that is the state
    every shipped template ships in, and it is safe only where the endpoint is not reachable
    from the internet. The Configuration → Connection check panel says which platforms are in that state
    and names any that already accepted an unsigned delivery.

## Endpoints

The webhook receiver listens on port **8081**:

```
POST /webhook           # platform auto-detected from headers/payload
POST /webhook/github    # explicit GitHub endpoint
POST /webhook/gitlab    # explicit GitLab endpoint
POST /webhook/jira      # explicit Jira endpoint
GET  /health            # liveness check
```

On the generic `/webhook` endpoint, the `X-GitHub-Event`, `X-Gitlab-Event`, etc. headers (and payload shape) route to the right handler; the explicit per-platform endpoints skip detection. `IWebhookHandler.CanHandle(platform, eventType)` selects the matching handler at dispatch time.

## Trigger Configuration

The trigger config (`pipeline_from_label`, `default_pipeline`, `done_status`, ...) lives on the tracker, and a project can override it field by field in a `github_trigger`/`gitlab_trigger`/`azuredevops_trigger`/`jira_trigger` block. See [Label-Based Triggers](../../trigger-it/labels.md) for the full shape and per-platform examples, and the [agentsmith.yml reference](agentsmith-yml.md#trigger-blocks) for every key.

## Pull request review label

On GitHub and GitLab, adding a label to a pull request or merge request can ask for a review, which runs `security-scan` on the pull request's head branch, for a repository a project configures. The label `security-review` always does. A project can add a word of its own with `pr_trigger_label` on its trigger block:

```yaml
projects:
  todolist:
    github_trigger:
      pr_trigger_label: needs-review
```

The word adds to `security-review`, it doesn't replace it. The project is found by the pull request's repository URL, so this needs the pull request's repo in the project's `repos:`.

## PR comment commands

Independent of the ticket lifecycle. A comment like `/agent-smith review` on a pull request or merge request starts a run directly, with no claim flow and no lifecycle labels. A comment may start `code`, `security-scan` or `pr-review` and nothing else; that list is fixed, because a comment is a lower-trust surface than your configuration.

Only an author with write access to the repository can issue one: on GitHub the payload's `author_association` says so, on GitLab and Azure DevOps the server asks the platform, which needs the token scopes listed under [Webhooks](../../trigger-it/webhooks.md#pull-request-comment-commands). A command from anyone else is ignored before any model reads it.

See [PR Comment Integration](../integrations/pr-comments.md) for command syntax.

## Per-Platform Setup

Per-platform walkthroughs (payload URLs, events to subscribe, secret placement in each platform's UI) live in [Webhooks](../../trigger-it/webhooks.md) — that page is canonical.

## Idempotency Guarantee

Webhook redelivery is safe. The claim is a lease in the database: the first delivery wins it, and a second delivery for the same ticket finds it held and starts nothing. One ticket never has two live runs.

This makes Agent Smith's webhook receiver tolerant of platform retry policies, network glitches, and operator-triggered redeliveries.

## Related

- [Ticket Lifecycle](../concepts/ticket-lifecycle.md) — what happens after the claim succeeds
- [Label-Based Triggers](../../trigger-it/labels.md) — trigger config shapes per platform
- [Polling](../../trigger-it/polling.md) — alternative ingress, and when to choose it
- [Webhooks](../../trigger-it/webhooks.md) — per-platform setup walkthroughs
- [PR Comments](../integrations/pr-comments.md) — free-form trigger path
