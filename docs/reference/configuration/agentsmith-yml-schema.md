# `agentsmith.yml` Schema Reference (p0139 catalogs)

The configuration file is split into **named catalogs** (agents, repos,
trackers, pipeline_triggers) and **projects** that reference catalog
entries by name. The same agent or tracker, defined once, can be used by
any number of projects.

The canonical machine-readable form is `config/agentsmith.schema.json`
(loaded automatically by editors via the `yaml-language-server` header).
A complete worked example is in `config/agentsmith.example.yml`.

If you're coming from a pre-p0139 config (per-project inline `source`,
`tickets`, `agent` blocks), the up-to-date catalog-based shape is in
[Connect your stuff: tracker pages](../../connect-your-stuff/tracker-azure-devops.md)
and [Repos: multi-repo](../../connect-your-stuff/repos-multi.md).

---

## Top level

| Key | Type | Required | Purpose |
|-----|------|----------|---------|
| `agents` | map<name, AgentConfig> | yes (if any project references one) | AI agent catalog |
| `repos` | map<name, RepoConnection> | yes (if any project references one) | Source repository catalog |
| `trackers` | map<name, TrackerConnection> | yes (if any project references one) | Issue/work-item tracker catalog |
| `pipeline_triggers` | map<label, pipeline-name> | no | Global label→pipeline default |
| `projects` | map<name, Project> | yes | Project entries |
| `secrets` | map<name, value> | no | Env-var-resolved secret references |
| `skills`, `sandbox`, `orchestrator`, `queue`, `limits`, `pipeline_storage`, `pipeline_data_flow` | object | no | Process-wide settings (unchanged from pre-p0139) |

Operator mistakes (unknown agent/tracker/repo references, duplicate
catalog keys, trigger blocks that don't match their tracker's type) are
caught by the validator at config-load time. The server refuses to start
with a single aggregated error message — there is no lazy fallback.

---

## `agents` — agent catalog

Each entry is the full `AgentConfig`. Reference by name from
`projects.<name>.agent`. Naming convention: kebab-case, often suffixed
with `-default` for entries used by multiple projects.

```yaml
agents:
  claude-default:
    type: Claude
    model: claude-sonnet-4-20250514
    cache: { is_enabled: true, strategy: automatic }
    models:
      primary: { model: claude-sonnet-4-20250514, max_tokens: 8192 }
```

Available `type`: `Claude`, `OpenAI`, `azure-openai`, `Gemini`, `Ollama`.
See `config/agentsmith.example.yml` for the full set of fields.

---

## `repos` — source repository catalog

```yaml
repos:
  acme-app:
    type: GitHub          # GitHub | GitLab | AzureDevOps | Local
    url: https://github.com/owner/acme-app
    auth: github_token    # name of an entry in `secrets:`

  acme-api-source:
    type: Local
    path: ./repo
    auth: none
```

`Local` repos use `path` (filesystem); remote types use `url`.

---

## `trackers` — issue/work-item tracker catalog

```yaml
trackers:
  acme-jira:
    type: Jira            # GitHub | GitLab | AzureDevOps | Jira
    url: https://acme.atlassian.net/
    auth: jira_token
    open_states: ["To Do", "In Progress"]
    done_status: "In Review"
    close_transition_name: "Done"
```

Lifecycle fields (`open_states`, `done_status`, `close_transition_name`,
`extra_fields`) belong on the tracker when shared across projects.
Project-specific lifecycle settings are an unsupported escape hatch
removed in p0139 — if you need different lifecycle states per project
against the same tracker install, define two tracker entries.

---

## `pipeline_triggers` — global label→pipeline map

```yaml
pipeline_triggers:
  agent-smith:init: init-project
  bug: fix-bug
  feature: add-feature
  security-review: security-scan
```

Used as the fallback when a project's `<provider>_trigger` block does not
declare its own `pipeline_from_label`. A populated project-level map
wins over this global default.

The map is global on purpose. Repeating the same 4-5 entries on every
project was the most common form of duplication in the old schema.

---

## `projects.<name>`

```yaml
projects:
  acme-app:
    agent: claude-default       # name from agents:
    tracker: acme-github        # name from trackers:
    repos: [acme-app]           # list of names from repos:
    pipeline: fix-bug
    coding_principles_path: .agentsmith/coding-principles.md
    github_trigger:
      trigger_statuses: ["open"]
      done_status: "closed"
      comment_keyword: "@agent-smith"
```

Required fields:
- `agent` (string — catalog name)
- `tracker` (string — catalog name)
- `repos` (list of catalog names — always a list, even when one)

Optional fields:
- `pipeline` (single string — legacy form)
- `pipelines` (list — multi-pipeline form)
- `default_pipeline`
- `coding_principles_path`, `skills_path`
- `github_trigger` / `gitlab_trigger` / `azuredevops_trigger` /
  `jira_trigger` (must match the tracker's `type` — the validator
  rejects mismatches)
- `polling` — alternative/complement to webhooks
- `sandbox`, `orchestrator` — per-project override blocks

`repos` is always a list. With p0139 it must contain exactly one entry
for consumers that haven't yet been migrated to iterate the list;
p0140 will activate multi-repo execution over the whole list.

### `repos` entry forms

Each `repos` item is one of three references, and all three may coexist
in one project:

| Form | Example | How it resolves |
|------|---------|-----------------|
| Catalog name | `acme-app` | Looked up in the top-level `repos:` catalog (legacy). |
| **Exact connection ref** | `acme-cloud/Service.Api` | **STATIC** (p0285): the git URL is built from the connection's type + host/org/project (+ repo name) with **no discovery call**. Loads even where repo discovery is unavailable (offline / CLI). |
| Connection glob | `acme-cloud/Service.*` (or `!acme-cloud/Service.Tests`) | **DISCOVERY** (p0281a): the connection's repos are enumerated from the provider API and filtered by the pattern; an over-broad glob matches whatever discovery finds. |

The split is by ref **shape**: a reference **without** a `*` is exact and
resolves statically; a reference **with** a `*` (or any `!`-exclude) keeps
the discovery path. Prefer exact refs when you want a clear, bounded,
offline-resolvable repo list; use a glob only when you deliberately want
"whatever repos match this pattern".

The built URL for an exact ref equals the URL discovery would produce for
the same repo (ADO: `https://dev.azure.com/{org}/{project}/_git/{name}`;
GitHub: `https://github.com/{owner}/{name}`; GitLab:
`{host|https://gitlab.com}/{group}/{name}`), so switching a repo from a
glob to an exact ref does not change in-flight runs.

### Per-repo `default_branch` override

Any `repos` item may be written as an object to override the default
branch for that one repo:

```yaml
repos:
  - acme-cloud/Service.Api                       # inherits the connection default_branch
  - { repo: acme-cloud/Docs, default_branch: main }   # docs repo on a different branch
```

Default-branch precedence for an exact connection ref: **per-repo
override → connection `default_branch` → the provider's real default at
clone time** (when no override and no connection default is set, the
source provider resolves the repo's actual default branch). The common
case — all repos share the connection's default branch — needs no
override at all.

---

## Common mistakes

| Error message | Cause |
|---------------|-------|
| `Project 'X' references agent 'Y' which is not defined in agents: catalog` | Typo in `agent:` value, or forgotten catalog entry |
| `Project 'X' references tracker 'Y' which is not defined in trackers: catalog` | Same for `tracker:` |
| `Project 'X' references repo 'Y' which is not defined in repos: catalog` | Same for `repos:` entries |
| `Project 'X': has jira_trigger but tracker 'Y' is type GitHub` | Trigger block on a project must match the tracker's type |
| `pipeline_triggers['Z'] references unknown pipeline 'W'` | Label maps to a pipeline name that doesn't exist (see `PipelinePresets.Names`) |

All errors are emitted in one pass at config-load — fix all of them and
restart, rather than one-at-a-time.
