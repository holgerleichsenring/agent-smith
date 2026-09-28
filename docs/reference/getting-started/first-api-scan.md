# First API Scan

!!! note "Which surface reads this"
    The YAML on this page is the file format. On a server the same values live in the database and are edited in the [Config studio](../../configure-it/config-studio.md); the CLI reads them from `agentsmith.yml`. `agent-smith config import` moves one into the other. See [Where configuration lives](../../configure-it/index.md).

Scan a running API for security vulnerabilities.

## Prerequisites

- Agent Smith [installed](../../get-it-running/install.md)
- `ANTHROPIC_API_KEY` (or other AI provider key)
- Docker running (for the Nuclei, Spectral and ZAP containers)
- A target API with a swagger.json endpoint

## Quick Run

```bash
export ANTHROPIC_API_KEY=sk-ant-...

agent-smith api-scan \
  --swagger https://your-api.com/swagger/v1/swagger.json \
  --target https://your-api.com \
  --output console
```

That's it. No config file needed for a basic scan.

## What happens

The scan runs the **api-security-scan** pipeline. Before any scanner runs, it states what it will look for. Deterministic scanners go first: [Nuclei](https://github.com/projectdiscovery/nuclei) probes the running API, [Spectral](https://github.com/stoplightio/spectral) lints the OpenAPI description with OWASP rules, and [OWASP ZAP](https://www.zaproxy.org/) exercises the target. The **api-security-master** then triages their output, with a read-only view of the source when one is available. Its findings are checked against the description, each one is put to a refuter, and the run ends with an account of which of its stated targets were answered. See [API Scan](../pipelines/api-scan.md) for the full pipeline.

## Output formats

```bash
# Console output (default)
agent-smith api-scan --swagger ./spec.json --target https://api --output console

# Markdown report (findings.md)
agent-smith api-scan --swagger ./spec.json --target https://api --output markdown --output-dir ./reports

# SARIF for GitHub Security tab (findings.sarif)
agent-smith api-scan --swagger ./spec.json --target https://api --output sarif --output-dir ./reports

# Multiple formats at once
agent-smith api-scan --swagger ./spec.json --target https://api --output console,markdown,sarif --output-dir ./reports
```

## Code-aware scans (optional)

For findings with file:line evidence, point the scan at the source:

```bash
agent-smith api-scan \
  --swagger https://your-api.com/swagger.json \
  --target https://your-api.com \
  --source-path .
```

Run with `--project` instead and the scan uses the project's first repository, a local path or a clone of a remote one. A missing path or a failed clone does not fail the scan; it runs without source.

## With a config file

For recurring scans, keep an `agentsmith.yml` and the scanner settings side by side:

```
.agentsmith/
├── agentsmith.yml
├── nuclei.yaml          # Nuclei tags, rate limit, time limit
├── spectral.yaml        # Spectral ruleset
└── zap.yaml             # ZAP time limit
```

```bash
agent-smith api-scan \
  --agent claude-scan \
  --swagger https://your-api.com/swagger.json \
  --target https://your-api.com \
  --config .agentsmith/agentsmith.yml \
  --output console,markdown
```

`--agent <name>` picks an agent from the config's `agents:` catalog directly, with no `--project` needed. The scanner files are found next to the `--config` file. See [Tool configuration](../pipelines/api-scan.md#tool-configuration).

## In CI/CD

See [CI/CD Integration](../cicd/index.md) for Azure DevOps, GitHub Actions, and GitLab examples.
