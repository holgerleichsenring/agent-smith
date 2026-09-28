# `agentsmith.yml` schema

The configuration file is split into **named catalogs** (agents,
connections, repos, trackers, MCP servers, secrets) and **projects** that
reference catalog entries by name. The same agent or tracker, defined once,
can be used by any number of projects. Names are matched without regard to
case.

The canonical machine-readable form is `config/agentsmith.schema.json`
(loaded automatically by editors via the `yaml-language-server` header).
A complete worked example is in `config/agentsmith.example.yml`, and every
key is listed in the [agentsmith.yml reference](agentsmith-yml.md).

---

## Top level

| Key | Type | Required | Purpose |
|-----|------|----------|---------|
| `agents` | map<name, agent> | if any project references one | AI agent catalog |
| `connections` | map<name, connection> | if any project references one | repo discovery scopes |
| `repos` | map<name, repo> | if any project references one | individual repository catalog |
| `trackers` | map<name, tracker> | if any project references one | issue/work-item tracker catalog |
| `mcp_servers` | map<name, server> | no | external MCP tool servers |
| `pipeline_triggers` | map<label, pipeline-name> | no | global label→pipeline fallback |
| `projects` | map<name, project> | yes | project entries |
| `secrets` | map<name, `${ENV}`> | no | env-var-resolved secret references |
| `persistence`, `auth`, `trace`, `tool_runner` | object | no | bootstrap and file-only blocks |
| `deployment`, `sandbox`, `orchestrator`, `registries`, `primary_provider`, `limits`, `pipeline_cost_cap`, `queue`, `dialogue`, `skills`, `pipeline_storage`, `pipeline_data_flow` | object | no | global settings |

Operator mistakes (unknown agent/tracker/repo references, trigger blocks
that don't match their tracker's type, template rules) are reported as
startup findings when the configuration loads. A blocking finding that
names a project stops that project's triggers; the server itself comes
up, so the configuration that fixes it stays reachable.

---

## `projects.<name>`

```yaml
projects:
  acme-app:
    agent: claude-default       # name from agents:
    tracker: acme-github        # name from trackers:
    repos: [acme-cloud/acme-app]
    resolution:
      tag: acme-app
```

Required fields:
- `agent` (catalog name)
- `tracker` (catalog name)
- `repos` (always a list, even with one entry)

Everything else (`resolution`, `templates`, `default_pipeline`,
`pipelines`, the per-platform trigger blocks, `sandbox`, `orchestrator`)
is listed in the [reference](agentsmith-yml.md#projects). A trigger block
must match the tracker's `type`; the validator rejects a `jira_trigger` on
a GitHub tracker.

### `repos` entry forms

Each `repos` item is one of three references, and all three may coexist
in one project:

| Form | Example | How it resolves |
|------|---------|-----------------|
| Catalog name | `acme-app` | Looked up in the top-level `repos:` catalog. |
| **Exact connection ref** | `acme-cloud/Service.Api` | **Static**: the git URL is built from the connection's type + host/org/project (+ repo name) with **no discovery call**. Loads even where repo discovery is unavailable (offline / CLI). |
| Connection rule | `acme-cloud/Service.*` (or `!acme-cloud/Service.Tests`) | **Discovery**: the connection's repos are enumerated from the provider API and filtered by the pattern; an over-broad rule matches whatever discovery finds. |

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

On GitLab, a discovered repo's name is its path relative to the
connection's group (`team-platform/Service.Api` for a project in a
subgroup), so an exact ref or a rule for a subgroup project includes the
subgroup: `acme-cloud/team-platform/Service.Api`.

### Per-repo `default_branch`

Any `repos` item may be written as an object to set a fallback default
branch for that one repo:

```yaml
repos:
  - acme-cloud/Service.Api                       # inherits the connection default_branch
  - { repo: acme-cloud/Docs, default_branch: main }   # docs repo on a different branch
```

Default-branch precedence: **the repository's own default branch, as
the platform reports it → the configured `default_branch` (per-repo item,
then connection) → `main`**. The configured value is a fallback for a
platform that can't answer (an empty repository, a failed call), not an
override. When it disagrees with what the repository reports, the run
logs a warning naming both. The common case needs no `default_branch`
at all.

### Per-repo `consumes` declaration

A `repos` item may also declare the served interface that repository
CONSUMES — the name the interface's own served description (its OpenAPI
`info.title`) carries:

```yaml
repos:
  - acme-cloud/Service.Api                            # serves the interface
  - { repo: acme-cloud/Storefront.Web, consumes: Orders }   # calls it
```

An api scan that holds a served description then reads the declared
consumer checkouts for call sites and reports what the interface offers
that no client exercises. A declared name that is not the interface the
run holds a description of FAILS the run, because a difference computed
over repositories the operator did not choose would read as a clean bill.
The declaration requires an exact (wildcard-free) repo reference.

---

## Common mistakes

| Error message | Cause |
|---------------|-------|
| `Project 'X' references agent 'Y' which is not defined in agents: catalog` | Typo in `agent:` value, or forgotten catalog entry |
| `Project 'X' references tracker 'Y' which is not defined in trackers: catalog` | Same for `tracker:` |
| `Project 'X' references repo 'Y' which is not defined in repos: catalog` | Same for `repos:` entries |
| `Project 'X': has jira_trigger but tracker 'Y' is type GitHub` | Trigger block on a project must match the tracker's type |
| `pipeline_triggers['Z'] references unknown pipeline 'W'` | Label maps to a pipeline name that doesn't exist |
| `A routing rule on project 'X' names pipeline 'fix-bug', which this product does not offer any more` | A retired pipeline name; write `code` |

All findings are reported in one pass when the configuration loads, and
`agent-smith config validate` prints the same list without starting a
server.
