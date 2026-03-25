# Configuration

Agent Smith is configured through a single YAML file and optional skill/tool definitions.

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

</div>

## File Discovery

Agent Smith searches for configuration in this order:

1. `--config` CLI flag (explicit path)
2. `agentsmith.yml` in current directory
3. `agentsmith.yaml` in current directory
4. `config/agentsmith.yml`

!!! tip
    Run `agent-smith init-project` to generate a starter `agentsmith.yml` with sensible defaults for your repository.
