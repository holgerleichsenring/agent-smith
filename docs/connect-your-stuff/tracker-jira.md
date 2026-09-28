# Tracker: Jira

Use this when your tickets live in Jira issues. The example here is the fictional `TodoList` product on `acme.atlassian.net`, in the Jira project `TL`.

## In the studio

Configuration for a server lives in the database and you edit it in the dashboard: switch the left rail to **Configuration** and work down the catalogs. The order matters, because each entry references the one before it: **Secrets** (names only), then **Agents**, then **Repositories**, then a **Tracker**, then a **Project** that wires them together. References are picked from dropdowns, so a project can't point at something that doesn't exist, and the drawer keeps **Create** disabled until every reference resolves.

Jira specifics: pick type `jira` on the tracker and the form asks for the site URL, the project key and the auth secret, then the workflow. Open states, done status, failed status, needs-clarification status, and the close transition name Jira needs to move an issue rather than just set a field. Further down are the label map ("Pipeline by label"), the "Default pipeline" a ticket runs when no label matched, and the three maps described below: lifecycle status names, label names, and work item kind by filing role.

The full tour is on [The Config studio](../configure-it/config-studio.md); what follows is the same wiring written as YAML, which is what the CLI reads directly and what `agent-smith config import` takes.

## The same thing as YAML


```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/holgerleichsenring/agent-smith/main/config/agentsmith.schema.json

agents:
  default-claude:
    type: claude
    models:
      scout:   { model: claude-haiku-4-5-20251001 }
      primary: { model: claude-sonnet-4-6 }
      planning:      { model: claude-sonnet-4-6 }
      summarization: { model: claude-haiku-4-5-20251001 }

repos:
  todolist-api:
    type: github
    url: https://github.com/acme-org/todolist-api
    auth: github_token
  todolist-worker:
    type: github
    url: https://github.com/acme-org/todolist-worker
    auth: github_token
  todolist-web:
    type: github
    url: https://github.com/acme-org/todolist-web
    auth: github_token

trackers:
  acme-jira:
    type: jira
    url: https://acme.atlassian.net
    project: TL
    auth: jira_token
    open_states: [Open, In Progress, To Do]
    done_status: Done
    close_transition_name: Done        # the transition Jira calls to reach done_status
    needs_clarification_status: Waiting for input
    default_pipeline: code             # what a ticket runs when no label matched
    work_item_kinds:                   # which issue type a ticket Agent Smith files becomes
      work: Story
      bug: Bug
    polling:
      enabled: false                   # use the webhook path; see Trigger it

projects:
  jira-todolist:
    agent: default-claude
    tracker: acme-jira
    repos: [todolist-api, todolist-worker, todolist-web]
    jira_trigger:
      secret: ${JIRA_WEBHOOK_SECRET}   # webhook shared secret — Jira is the one tracker where this lives in config
      project_resolution:
        strategy: tag
        value: todolist                # Jira labels are lowercase
      trigger_statuses: [Open, In Progress, To Do]
      done_status: Done
      pipeline_from_label:
        agent-smith:bug:                code
        agent-smith:feature:            code
        agent-smith:security-scan:      security-scan

secrets:
  claude_api_key: ${ANTHROPIC_API_KEY}
  github_token:   ${GITHUB_TOKEN}
  jira_token:     ${JIRA_TOKEN}
```

Jira-specific things to notice:

- **`close_transition_name`** — Jira doesn't expose a "set status" API. You move an issue between statuses by *transitioning* it, and transitions have names defined per project workflow. Set this to the transition that lands on your `done_status`. If you don't know it, look at the workflow diagram in Jira Settings → Issue Types → Workflows.
- **`jira_trigger.secret`** — the webhook shared secret. Jira is the exception among the trackers: GitHub / GitLab / Azure DevOps verify webhooks from server environment variables, but for Jira the secret sits in config, per project, under `jira_trigger`.
- **`jira_trigger.assignee_name`** — the Jira webhook starts work when an issue is *assigned* to this user. It defaults to `Agent Smith`; set it to the display name of the account the agent uses.
- **`lifecycle_status_names`** — by default the run lifecycle (pending / enqueued / in-progress / done / failed) is carried as labels. Add a `lifecycle_status_names:` map on the tracker to move issues through native Jira workflow statuses instead; labels remain the always-available carrier.
- **`label_names`** — renames the labels the framework writes, for a board with its own vocabulary. The keys are `pending`, `enqueued`, `in-progress`, `done`, `failed`, `waiting`, `shortfall` and `approved-set`; a key you leave out keeps its default word. Labels written under an older name are still recognised.
- **`work_item_kinds`** — which Jira issue type a ticket Agent Smith files is created as, per filing role: `work` (an approved cut's work ticket), `phase` (a single phase filed from a design conversation), `bug`, and `chat` (a ticket a chat request asked for). A role you don't map keeps the type it would have had anyway. The trail of the filing names the type it used. The lifecycle statuses have to exist in the workflow of that issue type; if they don't, the issue can't be moved and the refusal says the issue type is the usual cause.
- **`endpoints:`** — an override block on the tracker for individual REST paths, for the day Atlassian moves one. You should never need it until you do.
- **`polling.enabled: false`** — Atlassian Cloud webhooks are reliable; use them. Polling is per-tracker and is the fallback for Jira Server / Data Center behind a firewall.

The tracker owns the workflow: `open_states`, `done_status`, `failed_status` (where a failed run parks the issue), `needs_clarification_status`, `trigger_statuses` (falls back to `open_states`), `pipeline_from_label` and `default_pipeline` can all live on the tracker block, inherited by every project routed to it. When the label map has entries, a ticket matching none of them isn't routed; when it has none, every ticket runs the default pipeline, and if neither the project's trigger nor the tracker declares one it runs `code` and the startup findings say so. A project then only declares its resolution:

```yaml
projects:
  jira-todolist:
    agent: default-claude
    tracker: acme-jira
    repos: [todolist-api, todolist-worker, todolist-web]
    resolution:
      tag: todolist
    jira_trigger:
      secret: ${JIRA_WEBHOOK_SECRET}
```

The explicit `jira_trigger:` block from the full config works too and overrides the tracker field by field.

## Authentication

Jira's REST API authenticates with an email plus an API token. Create the token at `id.atlassian.com/manage-profile/security/api-tokens` and set both in the server's environment:

```bash
export JIRA_EMAIL=agent-smith@acme.org
export JIRA_TOKEN=...
```

The Jira connection reads these two variables directly (and `JIRA_URL` when the tracker has no `url`). The email is the account the agent acts as, and you'll see it in the issue history. The token is scoped to that account, so make sure it has permission to comment, transition, and label-edit issues in the project.

## How tickets reach Agent Smith

- **Webhook** (preferred). Jira Cloud posts to Agent Smith on issue updates, and an issue assigned to `assignee_name` starts work. The server listens on port 8081; point the webhook at `POST /webhook/jira` (or the generic `POST /webhook` — the platform is auto-detected). The shared secret is checked against `jira_trigger.secret`. Set up in [Webhooks: Jira](../trigger-it/webhooks.md#jira).
- **Polling**. For Jira Server / Data Center behind a firewall. Set `polling.enabled: true` and `interval_seconds: 60` (or more — Jira's API rate limits get strict).
- **Manual CLI**. `agent-smith code --ticket TL-54 --project jira-todolist` — note the project-prefixed issue key, that's Jira's native shape.

## What gets written back to the ticket

The database is the system of record; the issue status and labels are a best-effort projection of it.

When a run finishes:

- Issue transitions via `close_transition_name` to `done_status`.
- A new comment with the PR URLs and the run id.
- The `agent-smith:done` label gets added; `agent-smith:in-progress` removed.
- PRs whose verification came back red are opened as **drafts**.

When a run fails, the issue moves to `failed_status` if configured (otherwise the status stays), the `agent-smith:failed` label gets added, and a comment carries the error. With `label_names` set, your words replace the `agent-smith:*` ones.

When an issue is too thin to act on (title-only, or the run needs a decision), Agent Smith doesn't guess: it posts its open questions as an issue comment and parks the issue in `needs_clarification_status` (settable on the tracker or the project). A project whose pipeline can park a run and that has no such status gets a blocking startup finding, and its trigger doesn't run until you set one. Answering resumes the run — see [Spec dialogue](../how-it-works/spec-dialogue.md).

## Next

- [Repos: multi-repo](repos-multi.md) — TodoList wired across three GitHub repos with one Jira project as the tracker.
- [Webhooks: Jira](../trigger-it/webhooks.md#jira) — exact URL and verification.
- [AI providers](ai-providers.md) — if you don't want Claude.
- [Host it](../host-it/docker-compose.md).
