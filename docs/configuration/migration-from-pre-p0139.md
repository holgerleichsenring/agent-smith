# Migrating `agentsmith.yml` to the p0139 Catalog Schema

p0139 is a **breaking change** to `agentsmith.yml`. The pre-p0139 form
inlined `source:`, `tickets:`, and `agent:` blocks under every project;
the new form lifts those into top-level catalogs (`agents:`, `repos:`,
`trackers:`) and shrinks each project to references by name.

This guide walks through migrating one real project end to end. There is
**no automated migration tool**. You rewrite the file by hand once. The
server refuses to start against a pre-p0139 config — the loader rejects
projects that lack the new reference fields.

The full schema reference is in
[agentsmith-yml-schema.md](agentsmith-yml-schema.md).

---

## Worked example: `Sample-api-security-azure-openai`

The pre-p0139 form repeated the full `source`, `tickets`, and `agent` blocks
on every project that shared them with another project. For the Rhenus deployment
we operate two projects (`Sample-api-security-azure-openai` +
`rhetrust-api-security-azure-openai`) against the same Azure DevOps install,
running the same Azure-OpenAI agent. Pre-p0139, that's ~120 lines duplicated
across two project blocks.

### Before (pre-p0139)

```yaml
projects:
  Sample-api-security-azure-openai:
    source:
      type: AzureRepos
      url: https://RhenusITPD@dev.azure.com/RhenusITPD/Cloud%20Development/_git/RHS.Sample.Server
      auth: token
    tickets:
      type: AzureDevOps
      organization: RhenusITPD
      project: "Cloud Development"
      auth: token
    agent:
      type: azure-openai
      endpoint: https://oai-rhegpt-dev.openai.azure.com
      api_version: 2025-01-01-preview
      models:
        primary: { model: gpt-4.1, deployment: gpt4-1-deployment, max_tokens: 8192 }
        # ...50 more lines of model + pricing config
    pipeline: api-security-scan

  rhetrust-api-security-azure-openai:
    source:
      type: AzureRepos
      url: https://RhenusITPD@dev.azure.com/RhenusITPD/Cloud%20Development/_git/RHS.RheTrust.Server
      auth: token
    tickets:
      type: AzureDevOps
      organization: RhenusITPD
      project: "Cloud Development"
      auth: token
    agent:
      # ...full 50 lines of azure-openai config, repeated verbatim
    pipeline: api-security-scan
```

### After (p0139)

```yaml
agents:
  azure-openai-default:
    type: azure-openai
    endpoint: https://oai-rhegpt-dev.openai.azure.com
    api_version: 2025-01-01-preview
    models:
      primary: { model: gpt-4.1, deployment: gpt4-1-deployment, max_tokens: 8192 }
      # ...the model/pricing config lives here exactly ONCE

repos:
  Sample:
    type: AzureDevOps
    url: https://RhenusITPD@dev.azure.com/RhenusITPD/Cloud%20Development/_git/RHS.Sample.Server
    auth: azure_devops_token
  rhetrust:
    type: AzureDevOps
    url: https://RhenusITPD@dev.azure.com/RhenusITPD/Cloud%20Development/_git/RHS.RheTrust.Server
    auth: azure_devops_token

trackers:
  rhenus-cloud-dev:
    type: AzureDevOps
    organization: RhenusITPD
    project: "Cloud Development"
    auth: azure_devops_token

projects:
  Sample-api-security-azure-openai:
    agent: azure-openai-default
    tracker: rhenus-cloud-dev
    repos: [Sample]
    pipeline: api-security-scan

  rhetrust-api-security-azure-openai:
    agent: azure-openai-default
    tracker: rhenus-cloud-dev
    repos: [rhetrust]
    pipeline: api-security-scan
```

Adding a new project that uses the same agent + tracker is now four lines
(`agent: ...`, `tracker: ...`, `repos: [...]`, `pipeline: ...`).

---

## Migration steps

1. **List shared configurations.** Walk your existing `projects:`. For each
   `source`, `tickets`, `agent` block, write down whether it's used by
   exactly one project or by several. The shared ones become catalog entries.

2. **Build the `agents:` catalog.** For each distinct agent configuration
   (same `type`, `model`, `endpoint`, etc.), pick a kebab-case name and write
   one entry. Suggested suffixes: `-default` for the project's main agent,
   `-parallel` when only `parallelism.max_concurrent_skill_rounds` differs.

3. **Build the `repos:` catalog.** Each distinct repo URL → one entry.
   `type: AzureRepos` (pre-p0139) becomes `type: AzureDevOps`. Local repos
   (`type: Local, path: ./repo`) still work in the catalog.

4. **Build the `trackers:` catalog.** Each distinct tracker connection
   (org+project for AzureDevOps, host for Jira/GitHub/GitLab) → one entry.
   Per-tracker lifecycle config (`open_states`, `done_status`,
   `close_transition_name`, `extra_fields`) belongs here.

5. **Optional — build `pipeline_triggers:`.** If multiple projects today
   repeat the same `pipeline_from_label` map (very common — same five entries
   across every trigger block), lift it to the top-level
   `pipeline_triggers:` map. Per-project trigger blocks can then drop
   `pipeline_from_label:` and the global map applies as fallback. Projects
   that need different routing keep their own `pipeline_from_label` (it wins
   over the global default).

6. **Rewrite each project.** Replace inline `source:`, `tickets:`, `agent:`
   with three reference fields:
   ```yaml
   agent: <name from agents: catalog>
   tracker: <name from trackers: catalog>
   repos: [<name from repos: catalog>]
   ```
   `repos:` is **always a list**, even when there's only one repo —
   p0140 will activate parallel multi-repo execution over the list.
   Trigger blocks (`github_trigger`, `azuredevops_trigger`, …) stay on the
   project, unchanged.

7. **Start the server.** The validator runs at config-load and prints
   every problem in one pass:
   - typo in a reference name (`agent: claude-defaultt`)
   - duplicate key in a catalog
   - trigger block of the wrong type for the resolved tracker
   - `pipeline_triggers` entry referencing an unknown pipeline name

   Fix all of them and start again. There is no lazy fallback for missing
   references — the server refuses to start until the config is valid.

---

## Common gotchas

- **`AzureRepos` → `AzureDevOps`**: pre-p0139 source type `AzureRepos` is
  not accepted by the new schema. Use `AzureDevOps` for both the repo and
  the tracker; they're now separate catalog entries with their own type.

- **Trigger blocks must match the tracker type.** A project whose
  `tracker:` resolves to a Jira tracker can only declare `jira_trigger:`;
  declaring `github_trigger:` on it now fails at startup.

- **`pipeline_from_label` is optional.** Pre-p0139 needed it populated on
  every trigger block. p0139 falls back to the global `pipeline_triggers`
  map when omitted. Keep a per-project map only when this project actually
  needs different routing.

- **`repos:` is always a list.** Even one repo: `repos: [my-repo]`. The
  scalar form is rejected.

- **No backward-compat shim.** The loader does not accept the old per-project
  inline form alongside the new schema. You rewrite once, you don't
  half-migrate.

- **Auth references via `secrets:`.** Catalog entries' `auth:` field
  references a key in the top-level `secrets:` map (which resolves
  `${ENV_VAR}` placeholders). Same semantics as pre-p0139 — only the
  location moved.

---

## Validation messages

| Error | Cause | Fix |
|-------|-------|-----|
| `Project 'X' references agent 'Y' which is not defined in agents: catalog` | typo or missing entry | add `agents.Y:` or correct the reference |
| `Project 'X' references tracker 'Y' which is not defined in trackers: catalog` | same for trackers | add or correct |
| `Project 'X' references repo 'Y' which is not defined in repos: catalog` | same for repos | add or correct |
| `Project 'X': has jira_trigger but tracker 'Y' is type GitHub` | trigger block kind doesn't match tracker | use the right trigger block for the tracker type |
| `pipeline_triggers['Z'] references unknown pipeline 'W'` | global label→pipeline map points at a pipeline that doesn't exist | check `PipelinePresets.Names` for the canonical list |

All errors are emitted at once; the server logs every issue and refuses
to start until the file is valid.
