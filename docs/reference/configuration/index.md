# Configuration

This section documents the on-disk file format. Before you use it, know which surface actually reads it where you run.

!!! warning "A server is configured in the browser, not in this file"
    A long running server keeps its configuration in its database and edits it in the dashboard's [**Configuration studio**](../../configure-it/config-studio.md). From `agentsmith.yml` it reads the bootstrap slice at boot (`persistence:`, `secrets:` and the optional `auth:`) and ignores the rest. Catalog blocks written into a mounted ConfigMap have no effect there.

    The **CLI** is the other case. It reads this whole file, exactly as documented below.

    The two are bridged by `agent-smith config import` / `export`. [Where configuration lives](../../configure-it/index.md) has the full model and a per-block table.

## Configuration files

| File | Location | Purpose |
|------|----------|---------|
| **agentsmith.yml** | see below | agents, trackers, connections, repos, projects, secrets and the global settings |
| **context.yaml**, **principles.md** | `.agentsmith/contexts/<name>/` in each repository | what one component of a repo is and the rules it follows, see [Context file](../concepts/context-file.md) |
| **nuclei.yaml**, **spectral.yaml** | `config/` | scanner settings for the api-scan pipeline, see [Tool configuration](tools.md) |

## Pages

<div class="grid cards" markdown>

- :material-file-cog: **[agentsmith.yml reference](agentsmith-yml.md)** -- every block and key of the file
- :material-sitemap: **[agentsmith.yml schema](agentsmith-yml-schema.md)** -- the catalog model and how repo references resolve
- :material-routes: **[Project resolution](project-resolution.md)** -- how a ticket finds its project
- :material-account-group: **[Skills reference](skills.md)** -- the SKILL.md format
- :material-tag-multiple: **[Concept vocabulary](concept-vocabulary.md)** -- the concepts skills activate on
- :material-wrench: **[Tool configuration](tools.md)** -- Nuclei and Spectral config for the api-scan pipeline
- :material-webhook: **[Webhooks](webhooks.md)** -- endpoints, signature verification, PR comment commands
- :material-shield-check: **[Security scan config](security-scan.md)** -- DAST (ZAP), auto-fix, and trend analysis configuration

</div>

## File discovery

The CLI looks for `agentsmith.yml` in this order and takes the first one that exists:

1. `--config <path>`, when given
2. `.agentsmith/agentsmith.yml` in the current directory
3. `config/agentsmith.yml` in the current directory
4. `.agentsmith/agentsmith.yml` in your home directory

A server reads its bootstrap slice from `CONFIG_PATH`, which the container images set to `/app/config/agentsmith.yml`.
