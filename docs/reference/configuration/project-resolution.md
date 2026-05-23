# Project Resolution Strategies

When a ticket event arrives — by webhook or polling — Agent Smith has to answer one question: *which project owns this ticket?* The `project_resolution` block on every trigger configures the answer.

This page covers the four strategies (`tag`, `area-path`, `repo`, `to_address`), when to pick each, and a copy-pasteable YAML example for each. The shared mechanism (the `ProjectResolver` service, the `IncomingTicketEnvelope`, the matching pass) was introduced in p0140a; the per-strategy semantics are listed below.

> **Required on every trigger.** `project_resolution` lives inside the platform trigger block (`github_trigger`, `gitlab_trigger`, `azuredevops_trigger`, `jira_trigger`). Missing or unparseable values are rejected by the validator at config-load.

## How resolution runs

1. A ticket event arrives. The platform handler builds an `IncomingTicketEnvelope` containing the ticket id, labels, area path (ADO only), source repo URL (when known), and to-address (Email — p0141).
2. `ProjectResolver.Resolve(envelope)` walks every project's trigger and asks "does this project's `project_resolution` match this envelope?"
3. The match list is returned. **Zero matches** → structured log entry; optional tracker comment if `TrackerConnection.ZeroMatchComment` is configured (p0140b). **One match** → normal claim and spawn. **Two or more matches** → all projects claim and spawn in parallel; the `agent_smith_ambiguous_resolution_total` counter increments once per matched (project, pipeline). See [Multi-Repo Projects — Ambiguous-tag handling](multi-repo.md#ambiguous-tag-handling).

The four strategies differ only in *what* `Resolve` compares against. Their YAML shape is uniform:

```yaml
project_resolution:
  strategy: tag | area-path | repo | to_address
  value: <strategy-specific-string>
```

---

## `tag` — most common

A label on the ticket marks it for this project. The resolver matches when `envelope.Labels` contains the `value` string (case-sensitive, exact-match per label entry).

### When to use

The default choice. Works on all four trackers (GitHub, GitLab, Azure DevOps tags, Jira labels). Fits the multi-tenant pattern: one shared tracker hosting work for many teams, each team's project distinguished by a per-team tag.

### YAML example

A 5-project setup against one shared Jira install. Each project claims tickets tagged with its team slug:

```yaml
trackers:
  shared-jira:
    type: Jira
    url: https://acme.atlassian.net/
    auth: jira_token
    open_states: ["To Do", "In Progress"]
    done_status: "In Review"

projects:
  agentsmith-backend:
    agent: claude-default
    tracker: shared-jira
    repos: [backend-repo]
    jira_trigger:
      assignee_name: "Agent Smith"
      project_resolution:
        strategy: tag
        value: agentsmith-backend
      pipeline_from_label:
        bug: fix-bug
        feature: add-feature
      default_pipeline: fix-bug

  agentsmith-frontend:
    agent: claude-default
    tracker: shared-jira
    repos: [frontend-repo]
    jira_trigger:
      assignee_name: "Agent Smith"
      project_resolution:
        strategy: tag
        value: agentsmith-frontend
      pipeline_from_label:
        bug: fix-bug
      default_pipeline: fix-bug

  agentsmith-sdk:
    agent: claude-default
    tracker: shared-jira
    repos: [sdk-repo]
    jira_trigger:
      assignee_name: "Agent Smith"
      project_resolution:
        strategy: tag
        value: agentsmith-sdk
      pipeline_from_label:
        bug: fix-bug
      default_pipeline: fix-bug
```

### Worked example

A Jira issue is filed in the shared install with the labels `bug` + `agentsmith-backend`. Trigger flow:

1. Jira webhook arrives at `/webhook/jira`. `JiraAssigneeWebhookHandler` confirms the assignee is `Agent Smith` and builds an envelope with `Labels = ["bug", "agentsmith-backend"]`.
2. `ProjectResolver.Resolve` finds one match: project `agentsmith-backend` (its `project_resolution.value` is in the label list). The other two projects don't match.
3. `SpawnPipelineRunsUseCase` enqueues one `PipelineRequest` for `agentsmith-backend / fix-bug / backend-repo`.

If the issue had been labelled `bug + agentsmith-backend + agentsmith-sdk`, two projects would have matched. Both would have spawned. The `agent_smith_ambiguous_resolution_total` counter would have incremented twice (once per matched (project, pipeline)). Each run's Plan phase then decides whether the work is genuinely relevant for that repo.

> **Pitfall**: tag matching is case-sensitive on GitHub, GitLab, and Jira and is normalised to lowercase on Azure DevOps (ADO stores tags case-insensitively). When porting a project from one platform to another, double-check the `value` casing — a project that worked on ADO with `value: AgentSmithBackend` will silently match nothing on a migrated GitHub mirror.

---

## `area-path` — Azure DevOps only

The work item's `System.AreaPath` field matches `value`. Supports hierarchical match: a configured value matches itself **and** every sub-path below it.

### When to use

Multi-tenant Azure DevOps installs where one organisation/project hosts many teams' work, with each team owning a subtree of the area-path hierarchy. Common shape: one big ADO project for the whole company, area paths used to slice it.

`area-path` is not available on the other three trackers — the GitHub / GitLab / Jira `Ticket` entity has no area-path equivalent.

### YAML example

```yaml
trackers:
  contoso-ado:
    type: AzureDevOps
    url: https://dev.azure.com/contoso
    auth: ado_pat
    organization: contoso
    project: ContosoMain
    open_states: ["New", "Active", "Committed"]
    done_status: "In Review"

projects:
  contoso-billing:
    agent: claude-default
    tracker: contoso-ado
    repos: [billing-repo]
    azuredevops_trigger:
      project_resolution:
        strategy: area-path
        value: 'ContosoMain\\Billing'
      pipeline_from_label:
        bug: fix-bug
      default_pipeline: fix-bug
```

### Worked example

Work item filed under `ContosoMain\Billing\Invoicing` (a child of the configured area path).

1. ADO `workitem.updated` webhook arrives. `AzureDevOpsWorkItemWebhookHandler` builds an envelope with `AreaPath = "ContosoMain\\Billing\\Invoicing"`.
2. `ProjectResolver.Resolve` calls `AreaPathNormalizer.IsAtOrUnder(envelopePath, projectValue)`. `ContosoMain\Billing\Invoicing` is under `ContosoMain\Billing` → match.
3. `contoso-billing` claims the ticket and spawns its `fix-bug` pipeline against `billing-repo`.

A work item filed directly at `ContosoMain\Billing` (not a sub-path) also matches — exact-match is included in the hierarchical match.

A work item filed at `ContosoMain\Shipping` does **not** match — different subtree.

> **Pitfall — YAML escaping**: backslash is the area-path separator, and YAML treats `\\` inside a single-quoted string as a single literal backslash and `\\` inside a double-quoted string as a YAML escape sequence. Always write the value as a single-quoted string with the literal hierarchy, e.g. `'ContosoMain\Billing'`. If you use double quotes, you must escape each backslash: `"ContosoMain\\Billing"`. The deserialised string the resolver compares against is `ContosoMain\Billing` — single backslashes. Forgetting the escape produces a value that silently never matches any work item.

> **Slash-vs-backslash equivalence**: `AreaPathNormalizer` treats `/` and `\` identically, so `'ContosoMain/Billing'` and `'ContosoMain\Billing'` are equivalent. Pick one style and stick with it for readability.

---

## `repo` — single-repo GitHub URL identity

The source repo URL on the incoming envelope matches `value` exactly (after URL normalisation: scheme lowercased, trailing slash and `.git` stripped).

### When to use

Single-repo GitHub setups where the ticket is filed *on the repo itself* (a GitHub Issue), and you want the project resolution to key off the repo URL directly rather than a tag. This is the simplest, least-configurable strategy — there is nothing for the operator to label or tag.

### Validator constraint

`strategy: repo` requires `project.repos.length == 1`. The `value` is matched against the sole repo's URL. The validator rejects a project with `strategy: repo` and two or more `repos:` entries at config-load with:

```
Project 'X' uses project_resolution.strategy=repo but has 3 repos. The repo strategy is single-repo only — use 'tag' or 'area-path' for multi-repo projects.
```

Multi-repo projects must use `tag`, `area-path`, or `to_address`.

### YAML example

```yaml
repos:
  acme-cli:
    type: GitHub
    url: https://github.com/acme/cli
    auth: github_token

projects:
  acme-cli:
    agent: claude-default
    tracker: github-acme-cli   # GitHub ticket source is the same repo
    repos: [acme-cli]
    github_trigger:
      project_resolution:
        strategy: repo
        value: https://github.com/acme/cli
      pipeline_from_label:
        bug: fix-bug
        feature: add-feature
      default_pipeline: fix-bug
```

### Worked example

A GitHub issue is filed on `https://github.com/acme/cli` and labelled `bug`.

1. `issues` webhook arrives. `GitHubIssueWebhookHandler` builds an envelope with `SourceRepoUrl = "https://github.com/acme/cli"`.
2. `ProjectResolver.Resolve` matches `acme-cli` (URL identity, case-insensitive on scheme/host, trailing `.git` stripped).
3. `acme-cli` claims and spawns `fix-bug` against itself.

> **Pitfall**: `https://github.com/acme/cli` and `https://github.com/acme/cli.git` are normalised to the same value, but `git@github.com:acme/cli.git` (SSH form) is **not** — keep the `value` in HTTPS form, the same form the webhook payload carries.

---

## `to_address` — Email (forward reference)

The ticket's `to` address matches `value`. Used by the Email tracker introduced in p0141.

### Status

**Defined now, activated by p0141.** The strategy is parseable in `agentsmith.yml` today and survives config-load validation, but no incoming envelope populates the `ToAddress` field yet — the Email `ITicketProvider` lands in p0141. Configuring `strategy: to_address` against a non-Email tracker is rejected by the validator (the trigger block must match the tracker's type, see the [schema reference](agentsmith-yml-schema.md#common-mistakes)).

### YAML example (for when p0141 ships)

```yaml
trackers:
  support-mailbox:
    type: Email
    url: imap.acme.com
    auth: imap_credentials

projects:
  acme-support:
    agent: claude-default
    tracker: support-mailbox
    repos: [support-tools]
    email_trigger:
      project_resolution:
        strategy: to_address
        value: support@example.com
      pipeline_from_label:
        bug: fix-bug
      default_pipeline: fix-bug
```

### How it will work

The Email provider parses the `To:` header on the inbound message, normalises (lowercase, strip whitespace), and populates `IncomingTicketEnvelope.ToAddress`. `ProjectResolver` matches when the envelope's `ToAddress` equals the configured `value`. Multi-tenant mailboxes (`support@`, `bugs@`, `urgent@` all aliased to one inbox) resolve to different projects by to-address.

Watch p0141's release notes for activation.

---

## See also

- [Multi-Repo Projects](multi-repo.md) — fan-out behaviour, ambiguous-tag handling, init flow.
- [Metrics](../operations/metrics.md) — `agent_smith_ambiguous_resolution_total` and the cost-of-ambiguity dashboard.
- [agentsmith.yml Schema](agentsmith-yml-schema.md) — catalog reference; trigger-block / tracker-type cross-validation.
- [Webhooks](webhooks.md) — the ingress path that builds the `IncomingTicketEnvelope`.
- [Polling Setup](../setup/polling.md) — the alternative ingress. Polling supports `strategy: tag` only (the polled `Ticket` entity has Labels but not AreaPath / SourceRepoUrl / ToAddress).
