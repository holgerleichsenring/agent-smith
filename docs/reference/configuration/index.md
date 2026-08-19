# Configuration

This section documents the on-disk file format. Before you use it, know which surface actually reads it where you run.

!!! warning "A server is configured in the browser, not in this file"
    A long running server keeps its configuration in its database and edits it in the dashboard's [**Configuration studio**](../../configure-it/config-studio.md). From `agentsmith.yml` it reads two blocks at boot, `persistence:` and `secrets:`, and ignores the rest. Catalog blocks written into a mounted ConfigMap have no effect there.

    The **CLI** is the other case. It reads this whole file, exactly as documented below, and always has.

    The two are bridged by `agent-smith config import` / `export`. [Where configuration lives](../../configure-it/index.md) has the full model and a per-block table.

## Configuration Files

| File | Location | Purpose |
|------|----------|---------|
| **agentsmith.yml** | Project root | Main configuration: projects, AI provider, secrets, pipelines |
| **Skill YAMLs** | `config/skills/<category>/` | Role definitions for multi-agent discussions |
| **nuclei.yaml** | `config/` | Nuclei scanner settings for API security scans |
| **spectral.yaml** | `config/` | Spectral OpenAPI linter ruleset |

## Pages

<div class="grid cards" markdown>

- :material-file-cog: **[agentsmith.yml Reference](agentsmith-yml.md)** -- Full configuration reference with annotated examples
- :material-account-group: **[Skills Reference](skills.md)** -- Skill YAML format for multi-role agent discussions
- :material-wrench: **[Tool Configuration](tools.md)** -- Nuclei and Spectral config for the api-scan pipeline
- :material-webhook: **[Webhooks](webhooks.md)** -- GitHub webhook setup, signature verification, PR comment commands
- :material-shield-check: **[Security Scan Config](security-scan.md)** -- DAST (ZAP), auto-fix, and trend analysis configuration

</div>

## File discovery

The CLI (and the server, for its bootstrap slice) searches for configuration in this order:

1. `--config` CLI flag (explicit path)
2. `agentsmith.yml` in current directory
3. `agentsmith.yaml` in current directory
4. `config/agentsmith.yml`

!!! tip
    Run `agent-smith init-project` to generate a starter `agentsmith.yml` with sensible defaults for your repository.
