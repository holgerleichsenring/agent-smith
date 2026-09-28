# Project resolution strategies

!!! note "Which surface reads this"
    The YAML on this page is the file format. On a server the same values live in the database and are edited in the [Config studio](../../configure-it/config-studio.md) (the project drawer's **routing** tab); the CLI reads them from `agentsmith.yml`. `agent-smith config import` moves one into the other. See [Where configuration lives](../../configure-it/index.md).

When a ticket event arrives, by webhook or by polling, Agent Smith has to answer one question: *which project owns this ticket?* Each project answers it with one resolution rule.

This page covers the four strategies (`tag`, `area_path`, `repo`, `to_address`), when to pick each, and a YAML example for each.

## Where the rule lives

The short form sits on the project and names the strategy as its key:

```yaml
projects:
  todolist:
    agent: default-claude
    tracker: acme-jira
    repos: [acme/todolist-api]
    resolution:
      tag: todolist
```

The long form sits inside a platform trigger block (`github_trigger`, `gitlab_trigger`, `azuredevops_trigger`, `jira_trigger`), for a project that overrides its tracker's workflow anyway:

```yaml
    jira_trigger:
      project_resolution:
        strategy: tag
        value: todolist
```

Both produce the same rule. The trigger type always follows the tracker's type.

## How resolution runs

1. A ticket event arrives. The platform handler builds an `IncomingTicketEnvelope` with the ticket id, its labels, and, on a webhook, the area path (Azure DevOps) and the source repo URL where the platform sends one.
2. `ProjectResolver` walks every project's trigger and asks whether its rule matches the envelope.
3. **Zero matches**: a log line per project saying why it didn't match, and a tracker comment if the tracker has `zero_match_comment: true`. **One match**: the ticket is claimed and runs. **Two or more**: every matching project claims and runs, and the `agent_smith_ambiguous_resolution_total` counter goes up once per matched (project, pipeline).

Within a matched project, the tracker's label map picks the pipeline. When the map has entries and none matches, the ticket is dropped for that project, not sent to a default.

Two things stop a match before any rule is asked. A ticket carrying the `phase-epic` label marks a record rather than work and is never routed. And a trigger that carries a blocking startup finding (a missing park status, say) doesn't start runs until the finding is fixed.

Polling builds its envelope from the ticket alone, which has labels but no area path and no source repo. So on the polling path only `tag` can match.

---

## `tag`: the common one

A label on the ticket marks it for this project. The rule matches when any of the ticket's labels equals `value`, ignoring case.

### When to use

The default choice. Works on all four trackers (GitHub and GitLab labels, Azure DevOps tags, Jira labels), on webhooks and on polling. It fits the shared-tracker pattern: one board holding work for many teams, each team's project picked out by a per-team tag.

### YAML example

Three projects against one Jira site, each claiming tickets tagged with its own slug:

```yaml
trackers:
  shared-jira:
    type: jira
    url: https://acme.atlassian.net/
    project: TL
    auth: jira_token
    open_states: ["To Do", "In Progress"]
    done_status: "In Review"
    pipeline_from_label:
      bug: code
      feature: code

projects:
  todolist-backend:
    agent: claude-default
    tracker: shared-jira
    repos: [acme/todolist-api]
    resolution:
      tag: todolist-backend
  todolist-frontend:
    agent: claude-default
    tracker: shared-jira
    repos: [acme/todolist-web]
    resolution:
      tag: todolist-frontend
  todolist-sdk:
    agent: claude-default
    tracker: shared-jira
    repos: [acme/todolist-sdk]
    resolution:
      tag: todolist-sdk
```

### Worked example

A Jira issue carries the labels `bug` and `todolist-backend` and is assigned to the Agent Smith user.

1. The Jira webhook arrives at `/webhook/jira`. `JiraAssigneeWebhookHandler` confirms the assignee and builds an envelope with `Labels = ["bug", "todolist-backend"]`.
2. `ProjectResolver` finds one match, `todolist-backend`.
3. The tracker's label map turns `bug` into `code`, and one `code` run starts for `todolist-backend`.

Labelled `bug`, `todolist-backend` and `todolist-sdk`, the issue would have matched two projects and started two runs, and the ambiguity counter would have gone up twice.

---

## `area_path`: Azure DevOps, webhooks

The work item's `System.AreaPath` matches `value`, hierarchically: a configured path matches itself and every path below it, ignoring case.

### When to use

Azure DevOps organisations where one project hosts many teams' work and each team owns a subtree of the area-path hierarchy. Only the Azure DevOps webhook carries an area path; the other trackers, and polling, have none, so a project resolving by area path is reached by Azure DevOps webhooks only.

### YAML example

```yaml
trackers:
  contoso-ado:
    type: azure_devops
    organization: contoso
    project: ContosoMain
    auth: ado_pat
    open_states: ["New", "Active", "Committed"]
    done_status: "In Review"

projects:
  contoso-billing:
    agent: claude-default
    tracker: contoso-ado
    repos: [billing-repo]
    resolution:
      area_path: 'ContosoMain\Billing'
```

### Worked example

A work item filed under `ContosoMain\Billing\Invoicing` is updated.

1. The `workitem.updated` webhook arrives. `AzureDevOpsWorkItemWebhookHandler` builds an envelope with that area path.
2. `ContosoMain\Billing\Invoicing` is under `ContosoMain\Billing`, so `contoso-billing` matches.

A work item filed directly at `ContosoMain\Billing` matches too. One at `ContosoMain\Shipping` doesn't.

> **Pitfall, YAML escaping**: backslash is the area-path separator. Write the value single-quoted with single backslashes, `'ContosoMain\Billing'`. In double quotes every backslash has to be doubled, `"ContosoMain\\Billing"`. `/` and `\` are treated alike, so `'ContosoMain/Billing'` works as well and dodges the problem.

---

## `repo`: the ticket's own repo, single-repo projects

The ticket's source repo URL, as the webhook payload carries it, equals the URL of the project's only repository (compared ignoring case). The configured value names the rule; what is compared is the project's repo.

### When to use

A single-repo GitHub or GitLab project whose issues are filed on the repo itself, where you'd rather not label anything. Webhooks only, since a polled ticket carries no source repo.

### Validator constraint

`repo` needs a project with exactly one entry in `repos:`. A project with more is refused when the configuration loads:

```
Project 'X': github project_resolution.strategy=repo requires exactly one entry in repos (has 3). Use strategy=tag or area_path for multi-repo projects, since the webhook payload's repo URL alone cannot disambiguate which repo of the project the ticket belongs to.
```

### YAML example

```yaml
repos:
  acme-cli:
    type: github
    url: https://github.com/acme-org/cli
    auth: github_token

projects:
  acme-cli:
    agent: claude-default
    tracker: acme-cli-issues     # a GitHub tracker on the same repo
    repos: [acme-cli]
    resolution:
      repo: https://github.com/acme-org/cli
```

> **Pitfall**: the comparison is exact apart from case. Configure the repo with the same URL form the webhook sends (`https://github.com/acme-org/cli`); a `.git` suffix or an SSH URL won't match.

---

## `to_address`: reserved

The ticket's to-address equals `value`, ignoring case. The strategy parses and validates, but none of the four trackers delivers a to-address, so a project resolving by it matches nothing today.

---

## See also

- [Repos: multi-repo](../../connect-your-stuff/repos-multi.md), the multi-repo project model.
- [Metrics](../operations/metrics.md), `agent_smith_ambiguous_resolution_total`.
- [agentsmith.yml reference](agentsmith-yml.md#projects), every project key.
- [Webhooks](webhooks.md), the ingress path that builds the envelope.
- [Polling](../../trigger-it/polling.md), the other ingress, where only `tag` applies.
